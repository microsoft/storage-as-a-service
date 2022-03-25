using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.UsEduCsu.Saas
{
	public static class TopLevelFolders
	{
		[FunctionName("TopLevelFoldersGET")]
		public static async Task<IActionResult> TopLevelFoldersGET(
			[HttpTrigger(AuthorizationLevel.Function, "GET", Route = "TopLevelFolders/{account}/{filesystem}/{user?}")]
			HttpRequest req, string account, string filesystem, string user, ILogger log)
		{
			// Check for logged in user
			ClaimsPrincipal claimsPrincipal;

			try
			{
				claimsPrincipal = UserOperations.GetClaimsPrincipal(req);
				if (Services.Extensions.AnyNull(claimsPrincipal, claimsPrincipal.Identity))
					return new BadRequestErrorMessageResult("Call requires an authenticated user.");
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				return new BadRequestErrorMessageResult("Unable to authenticate user.");
			}

			var authenticatedUser = claimsPrincipal.Identity.Name;
			var principalId = UserOperations.GetUserPrincipalId(claimsPrincipal);

			// TODO: Review for security. This seems to allow any authenticated user to pass another user's UPN and retrieve the folders they have access to?
			// Perhaps acceptable if using an "admin" role
			if (user == null)
				user = authenticatedUser;

			// Find out user who is calling
			var storageUri = SasConfiguration.GetStorageUri(account);
			var tokenCredential = new DefaultAzureCredential();

			// Get User Credentials
			var userCred = CredentialHelper.GetUserCredentials(log, principalId);
			var folderOperations = new FolderOperations(storageUri, filesystem, log,
				tokenCredential);

			// Retrieve all folders in the container
			var folderList = folderOperations.GetFolderList();

			// Filter for ALL the top-level folders in the container that are accessible by the user
			var folderOperationsAsUser = new FolderOperations(storageUri, filesystem, log,
				userCred);

			var folders = folderOperationsAsUser.GetAccessibleFolders(folderList);

			// Retrieve the container's data plane RBAC role assignments for the calling user
			// TODO: Possible improvement: if the calling user is the owner per RBAC (or any RBAC data plane role?),
			// simply retrieve all folders instead of checking each folder (done in the the call above)?
			var roleOperations = new RoleOperations(log, tokenCredential);
			var roles = roleOperations.GetContainerRoleAssignments(account, principalId)
									.Where(ra => ra.Container == filesystem);

			// TODO: Why only for the Owner data plane role?
			// If the calling user has the RBAC data plane owner role
			if (roles.Any(ra => ra.RoleName.Contains("Owner")))
			{
				// Add Root Folder if they are the owner
				var fd = folderOperations.GetFolderDetail(string.Empty);

				if (fd != null)
				{
					fd.Name = "{root}";
					fd.UserAccess = new List<string>(roles.Select(ra => $"{ra.RoleName}: {authenticatedUser}"));
					folders.Add(fd);
				}
			}

			// Sort folders for display
			var sortedFolders = folders
								.Where(f => f != null)
								.OrderBy(f => f.URI)
								.ToList();

			return new OkObjectResult(sortedFolders);
		}

		[ProducesResponseType(typeof(FolderOperations.FolderDetail), StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[FunctionName("TopLevelFoldersPOST")]
		public static async Task<IActionResult> TopLevelFoldersPOST(
				[HttpTrigger(AuthorizationLevel.Function, "POST", Route = "TopLevelFolders/{account}/{filesystem}")]
				HttpRequest req, string account, string filesystem, ILogger log)
		{
			// Check for logged in user
			ClaimsPrincipal claimsPrincipal;
			try
			{
				claimsPrincipal = UserOperations.GetClaimsPrincipal(req);
				if (Services.Extensions.AnyNull(claimsPrincipal, claimsPrincipal.Identity))
					return new BadRequestErrorMessageResult("Call requires an authenticated user.");
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				return new BadRequestErrorMessageResult("Unable to authenticate user.");
			}

			// Extracting body object from the call and deserializing it.
			var tlfp = await GetTopLevelFolderParameters(req, log);
			if (tlfp == null)
				return new BadRequestErrorMessageResult($"{nameof(TopLevelFolderParameters)} is missing.");

			// Add Route Parameters
			tlfp.StorageAcount ??= account;
			tlfp.FileSystem ??= filesystem;

			// Authorize the calling user as owner of the container
			var roleOperations = new RoleOperations(log, new DefaultAzureCredential());
			// TODO: Enhance the GetContainerRoleAssignments method to allow passing in a container name
			var roles = roleOperations.GetContainerRoleAssignments(account, UserOperations.GetUserPrincipalId(claimsPrincipal))
							.Where(ra => ra.Container == tlfp.FileSystem)
							.ToList();

			// If the calling user does not have the Storage Blob Data Owner RBAC role on the container
			// TODO: Consider switching to user credentials?
			if (!roles.Any(ra => ra.RoleName.Contains("Owner")))
			{
				// TODO: Should be an HTTP 403
				return new BadRequestErrorMessageResult("Must be a member of the Storage Blob Data Owner role on the file system to create Top-Level Folders.");
			}

			// Check Parameters
			string error = null;
			if (Services.Extensions.AnyNullOrEmpty(tlfp.FileSystem, tlfp.Folder, tlfp.FolderOwner, tlfp.FundCode, tlfp.StorageAcount))
			{
				error = $"{nameof(TopLevelFolderParameters)} is malformed.";
				return new BadRequestErrorMessageResult(error);
			}

			// Call each of the steps in order and error out if anything fails
			Result result = null;
			var storageUri = SasConfiguration.GetStorageUri(account);
			TokenCredential ApiCredential = new DefaultAzureCredential();
			var fileSystemOperations = new FileSystemOperations(log, ApiCredential, storageUri);
			var folderOperations = new FolderOperations(storageUri, tlfp.FileSystem, log, ApiCredential);

			// Create Folders and Assign permissions
			result = await folderOperations.CreateNewFolder(tlfp.Folder);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Folder Metadata
			result = await folderOperations.AddMetaData(tlfp.Folder, tlfp.FundCode, tlfp.FolderOwner);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Folder permissions
			if (tlfp.UserAccessList.Count == 0)
				tlfp.UserAccessList.Add(tlfp.FolderOwner);

			// Convert UserAccessList to Object Ids (both users and groups)
			var objectAccessList = await ConvertToObjectId(log, tlfp.UserAccessList);
			result = await folderOperations.AssignFullRwx(tlfp.Folder, objectAccessList);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Pull back details for display
			var folderDetail = folderOperations.GetFolderDetail(tlfp.Folder);

			return new OkObjectResult(folderDetail) { StatusCode = StatusCodes.Status201Created };
		}

		private static async Task<Dictionary<string, AccessControlType>> ConvertToObjectId(ILogger log, List<string> userAccessList)
		{
			var tokenCredential = new DefaultAzureCredential();
			var userOperations = new UserOperations(log, tokenCredential);
			var groupOperations = new GroupOperations(log, tokenCredential);

			var objectList = new Dictionary<string, AccessControlType>();

			ParallelOptions pOptions = new();

			await Parallel.ForEachAsync(userAccessList, pOptions, async (item, cToken) =>
			{
				if (cToken.IsCancellationRequested)
					return;

				// If this access control entry is a UPN
				if (IsUpn(item))
				{
					// Translate the UPN into a principal ID
					var uid = await userOperations.GetObjectIdFromUPN(item);
					if (uid != null)
						objectList.Add(uid, AccessControlType.User);
				}
				else
				{
					// Assume it's a group name
					var gid = await groupOperations.GetObjectIdFromGroupName(item);
					if (gid != null)
						objectList.Add(gid, AccessControlType.Group);
				}

				// TODO: consider keeping track of which ACEs could not be translated and reporting back to user
			});

			return objectList;
		}

		private static bool IsUpn(string input)
		{
			return input != null
				&& input.Contains('@');
		}

		internal static async Task<TopLevelFolderParameters> GetTopLevelFolderParameters(HttpRequest req, ILogger log)
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
			var tlfp = JsonConvert.DeserializeObject<TopLevelFolderParameters>(body);

			return tlfp;
		}

		internal class TopLevelFolderParameters
		{
			internal TopLevelFolderParameters()
			{
				UserAccessList = new List<string>();
			}

			public string StorageAcount { get; set; }

			public string FileSystem { get; set; }

			public string Folder { get; set; }

			public string FundCode { get; set; }

			public string FolderOwner { get; set; }        // Probably will not stay as a string

			public List<string> UserAccessList { get; set; }
		}
	}
}
