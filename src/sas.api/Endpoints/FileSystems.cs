using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Authorization.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using sas.api.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace sas.api
{
	public static class FileSystems
	{
		[FunctionName("FileSystems")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "POST", "GET", Route = "FileSystems/{account?}")]
			HttpRequest req, ILogger log, String account)
		{
			if (req.Method == HttpMethods.Post)
				return await FileSystemsPOST(req, log, account);

			if (req.Method == HttpMethods.Get)
				return await FileSystemsGET(req, log, account);

			// TODO: If this is even possible (accepted methods are defined above?) return HTTP error code 405, response must include an Allow header with allowed methods
			return null;
		}

		private static async Task<IActionResult> FileSystemsGET(HttpRequest req, ILogger log, string account)
		{
			// Check for logged in user
			ClaimsPrincipal claimsPrincipal;
			try
			{
				claimsPrincipal = UserOperations.GetClaimsPrincipal(req);
				if (Extensions.AnyNull(claimsPrincipal, claimsPrincipal.Identity))
					return new BadRequestErrorMessageResult("Call requires an authenticated user.");
			}
			catch (Exception ex)
			{
				log.LogError(ex.Message);
				return new BadRequestErrorMessageResult("Unable to authenticate user.");
			}

			// Calculate UPN
			var upn = claimsPrincipal.Identity.Name.ToLowerInvariant();
			var principalId = claimsPrincipal.Claims
				.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?
				.Value;

			// Get the Containers for a upn from each storage account
			// TODO: Check for account != null first
			var accounts = SasConfiguration.GetConfiguration().StorageAccounts;
			if (account != null)
				accounts = accounts.Where(a => a.ToLowerInvariant() == account).ToArray();

			var x = new RoleOperations(log);

			// Define the return value
			var result = new List<FileSystemResult>();

			foreach (var acct in accounts)
			{
				var containers = new List<string>();

				// Get containers for which principal has data plane RBAC assignment
				// TODO: Expand to include all containers if principal has Storage Blob * assignment on account as a whole
				try
				{
					var containerRoles = x.GetContainerRoleAssignments(acct, principalId);

					// If any direct RBAC assignments exist on containers in this account
					if (containerRoles?.Count > 0)
					{
						containers.AddRange(containerRoles.Select(s => s.Container));
					}
				}
				catch (ErrorResponseException ex) when (ex.Message.Contains("'Forbidden'"))
				{
					// It's likely the App Registration doesn't have the proper permission at the storage account level
					log.LogError(ex, $"Error retrieving RBAC permissions for storage account {acct}: {ex.Message}");
					// Skip this storage account entirely
					continue;
				}
				catch (Exception ex)
				{
					log.LogError(ex, ex.Message);
					containers.Add(ex.Message);
				}

				// TODO: Centralize this to account for other clouds
				var serviceUri = new Uri($"https://{acct}.dfs.core.windows.net");
				var adls = new FileSystemOperations(serviceUri, log);

				// Get containers for which principal has ACL assignment
				try
				{
					var clist = adls.GetContainersForUpn(upn).ToArray();
					containers.AddRange(clist);
				}
				catch (Exception ex)
				{
					log.LogError(ex, ex.Message);
					containers.Add(ex.Message);
				}

				// Add the current account and the permissioned containers to the result set
				result.Add(new FileSystemResult()
				{
					Name = acct,
					FileSystems = containers.Distinct().OrderBy(c => c).ToList()
				});
			}

			log.LogTrace(JsonConvert.SerializeObject(result, Formatting.None));

			// Send back the Accounts and FileSystems
			return new OkObjectResult(result);
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
			if (Extensions.AnyNull(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode, tlfp.StorageAcount))
				error = $"{nameof(FileSystemParameters)} is malformed.";
			if (tlfp.Owner.Contains("#EXT#"))
				error = "Guest accounts are not supported.";
			if (error != null)
				return new BadRequestErrorMessageResult(error);

			// Get Blob Owner
			var ownerId = await UserOperations.GetObjectIdFromUPN(tlfp.Owner);
			if (ownerId == null)
				return new BadRequestErrorMessageResult("Owner identity not found. Please verify that the Owner is a valid member UPN and that the application has User.Read.All permission in the directory.");

			// Call each of the steps in order and error out if anytyhing fails
			var storageUri = new Uri($"https://{tlfp.StorageAcount}.dfs.core.windows.net");
			var fileSystemOperations = new FileSystemOperations(storageUri, log);

			// Create File System
			Result result = await fileSystemOperations.CreateFileSystem(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Add Blob Owner
			var roleOperations = new RoleOperations(log);
			roleOperations.AssignRoles(tlfp.StorageAcount, tlfp.FileSystem, ownerId);

			// Get Root Folder Details
			var folderOperations = new FolderOperations(storageUri, tlfp.FileSystem, log);
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
			var bodyDeserialized = JsonConvert.DeserializeObject<FileSystemParameters>(body);
			return bodyDeserialized;
		}

		private class FileSystemResult
		{
			//[{name: 'adlstorageaccountname', fileSystems: [{name: 'file system name'}]]
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
