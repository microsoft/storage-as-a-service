using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.UsEduCsu.Saas
{
	public static class FileSystems
	{
		public static ILogger logger;

		[FunctionName("FileSystems")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "POST", "GET", Route = "FileSystems/{account?}")]
			HttpRequest req,
			ILogger log, String account)
		{

			if (req.Method == HttpMethods.Post)
				return await FileSystemsPOST(req, log, account);
			else if (req.Method == HttpMethods.Get)
				return FileSystemsGET(req, log, account);

			// TODO: If this is even possible (accepted methods are defined above?) return HTTP error code 405, response must include an Allow header with allowed methods
			return null;
		}

		private static IActionResult FileSystemsGET(HttpRequest req, ILogger log, string account)
		{
			// Check for logged in user
			ClaimsPrincipal claimsPrincipal;
			try
			{
				claimsPrincipal = UserOperations.GetClaimsPrincipal(req);
				if (Services.Extensions.AnyNull(claimsPrincipal, claimsPrincipal.Identity))
					// TODO: Consider return HTTP 401 instead of HTTP 500
					return new BadRequestErrorMessageResult("Call requires an authenticated user.");
			}
			catch (Exception ex)
			{
				log.LogError(ex.Message);
				return new BadRequestErrorMessageResult("Unable to authenticate user.");
			}

			// Calculate UPN
			var upn = claimsPrincipal.Identity.Name.ToLowerInvariant();
			// TODO: principalId could be null
			var principalId = claimsPrincipal.Claims
				.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?
				.Value;

			// Get the Containers for a upn from each storage account
			var accounts = SasConfiguration.GetConfiguration().StorageAccounts;
			if (account != null)
				accounts = accounts.Where(a => a.ToLowerInvariant() == account).ToArray();

			// Define the return value
			var result = new List<FileSystemResult>();

			Parallel.ForEach(accounts, acct =>
			{
				// TODO: Consider renaming to GetPermissionedContainers
				var containers = GetContainers(log, acct, upn, principalId);

				// If the current user has access to any containers in the current storage account
				if (containers.Any())
				{
					// Create a result object for the current storage account
					var fs = new FileSystemResult()
					{
						Name = acct,
						FileSystems = containers.Distinct().OrderBy(c => c).ToList()
					};

					// Add the current account and the permissioned containers to the result set
					result.Add(fs);
				}
			});

			log.LogTrace(JsonSerializer.Serialize(result));

			// Send back the Accounts and FileSystems
			return new OkObjectResult(result);
		}

		/// <summary>
		/// Retrieves the containers in the specified storage account to which the specified account has access.
		/// Access can be either via RBAC data plane roles or via ACLs on folders.
		/// The UPN and the principal ID must refer to the same account.
		/// </summary>
		/// <param name="log"></param>
		/// <param name="account"></param>
		/// <param name="upn"></param>
		/// <param name="principalId"></param>
		/// <returns>The list of containers to which the specified principal has access.</returns>
		private static IList<string> GetContainers(ILogger log, string account, string upn, string principalId)
		{
			// Define the return value (never return null)
			var containers = new List<string>();

			var serviceUri = SasConfiguration.GetStorageUri(account);
			var adls = new FileSystemOperations(log, new DefaultAzureCredential(), serviceUri);

			// Retrieve all the containers in the specified storage account
			var fileSystems = adls.GetFilesystems();

			// Transition to User Credentials
			var userCred = CredentialHelper.GetUserCredentials(log, principalId);

			var roleOperations = new RoleOperations(log, new DefaultAzureCredential());

			// Check for RBAC data plane access to any container in the account
			var containerDataPlaneRoleAssignments = roleOperations
							.GetContainerRoleAssignments(account, principalId);
			// HACK: Doing this in a loop is unnecessary
			//.Where(ra => ra.Container == filesystem.Name);
			// HACK: Unncessary, GetContainerRoleAssignments already performs this check
			//			&& ra.PrincipalId == principalId);

			// If the specified principal has any data plane RBAC assignment on any container
			if (containerDataPlaneRoleAssignments.Any())
			{
				// They have access to the container
				containerDataPlaneRoleAssignments.ForEach(r => containers.Add(r.Container));
			}

			// For any containers where the principal doesn't have a data plane RBAC role
			Parallel.ForEach(fileSystems.Where(fs => !containers.Any(c => c == fs.Name)), filesystem =>
			{
				// Evaluate top-level folder ACLs
				// Check if user can read folders
				var folderOps = new FolderOperations(log, userCred, serviceUri, filesystem.Name);
				var folders = folderOps.GetAccessibleFolders(checkForAny: true);

				if (folders.Count > 0)
					containers.Add(filesystem.Name + " (by ACL)");
			});

			return containers;
		}

		private static async Task<IActionResult> FileSystemsPOST(HttpRequest req, ILogger log, string account)
		{
			if (!SasConfiguration.ValidateSharedKey(req, SasConfiguration.ApiKey.FileSystems))
			{
				return new UnauthorizedResult();
			}

			// Extracting body object from the call and deserializing it.
			var tlfp = await GetFileSystemParameters(req, log);
			if (tlfp == null)
				return new BadRequestErrorMessageResult($"{nameof(FileSystemParameters)} is missing.");

			// Add Route Parameters
			tlfp.StorageAcount ??= account;

			// Check Parameters
			string error = null;
			if (Services.Extensions.AnyNull(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode, tlfp.StorageAcount))
				error = $"{nameof(FileSystemParameters)} is malformed.";
			if (tlfp.Owner.Contains("#EXT#"))
				error = "Guest accounts are not supported.";
			if (error != null)
				return new BadRequestErrorMessageResult(error);

			// Setup Azure Credential
			var tokenCredential = new DefaultAzureCredential();

			// Get Blob Owner
			var userOperations = new UserOperations(log, tokenCredential);
			var ownerId = await userOperations.GetObjectIdFromUPN(tlfp.Owner);
			if (ownerId == null)
				return new BadRequestErrorMessageResult("Owner identity not found. Please verify that the Owner is a valid member UPN and that the application has User.Read.All permission in the directory.");

			// Call each of the steps in order and error out if anytyhing fails
			var storageUri = SasConfiguration.GetStorageUri(tlfp.StorageAcount);
			var fileSystemOperations = new FileSystemOperations(log, tokenCredential, storageUri);

			// Create File System
			Result result = await fileSystemOperations.CreateFileSystem(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Assign Other Execute Permission
			result = await fileSystemOperations.SetRootOtherACL(tlfp.FileSystem);

			// Add Blob Owner
			var roleOperations = new RoleOperations(log, tokenCredential);
			roleOperations.AssignRoles(tlfp.StorageAcount, tlfp.FileSystem, ownerId);

			// Get Root Folder Details
			var folderOperations = new FolderOperations(log, tokenCredential, storageUri, tlfp.FileSystem);
			var folderDetail = folderOperations.GetFolderDetail(String.Empty);

			return new OkObjectResult(folderDetail);
		}

		internal static async Task<FileSystemParameters> GetFileSystemParameters(HttpRequest req, ILogger log)
		{
			string body = string.Empty;
			using (var reader = new StreamReader(req.Body, Encoding.UTF8))
			{
				body = await reader.ReadToEndAsync();
				if (string.IsNullOrEmpty(body))
				{
					log.LogError("Body was empty coming from ReadToEndAsync");
				}
			}
			var bodyDeserialized = JsonSerializer.Deserialize<FileSystemParameters>(body);
			return bodyDeserialized;
		}

		private class FileSystemResult
		{
			public string Name { get; set; }

			public List<string> FileSystems { get; set; }
		}

		internal class FileSystemParameters
		{
			public string StorageAcount { get; set; }

			public string FileSystem { get; set; }

			public string FundCode { get; set; }

			public string Owner { get; set; }        // Probably will not stay as a string
		}
	}
}
