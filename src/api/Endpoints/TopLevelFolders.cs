using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Collections.Generic;
using Azure.Core;
using Azure.Storage.Files.DataLake.Models;

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

			// Get User Credentials
			var userCred = CredentialHelper.GetUserCredentials(log, principalId);
			var folderOperations = new FolderOperations(log, userCred, storageUri, filesystem);
			// Retrieve ALL top-level folders in the container that are accessible by the user
			var folders = folderOperations.GetAccessibleFolders();

			// Add Root Folder if they are the owner
			// TODO: Possible improvement: if they are the owner per RBAC (or any RBAC data plane role?), simply retrieve all folders instead of checking each folder?
			var roleOperations = new RoleOperations(log, new DefaultAzureCredential());
			var roles = roleOperations.GetContainerRoleAssignments(account, principalId)
									.Where(ra => ra.Container == filesystem
											&& ra.PrincipalId == principalId);

			// TODO: Why only for the Owner data plane role?
			if (roles.Any(ra => ra.RoleName.Contains("Owner")))
			{
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
			var roles = roleOperations.GetContainerRoleAssignments(account, UserOperations.GetUserPrincipalId(claimsPrincipal))
							.Where(ra => ra.Container == tlfp.FileSystem).ToList();
			if (roles.Count() == 0 || roles.Any(ra => !ra.RoleName.Contains("Owner")))
				return new BadRequestErrorMessageResult("Must be an Owner of the file system to create Top Level Folders.");

			// Check Parameters
			string error = null;
			if (Services.Extensions.AnyNull(tlfp.FileSystem, tlfp.Folder, tlfp.FolderOwner, tlfp.FundCode, tlfp.StorageAcount))
				error = $"{nameof(TopLevelFolderParameters)} is malformed.";

			// Call each of the steps in order and error out if anytyhing fails
			Result result = null;
			var storageUri = SasConfiguration.GetStorageUri(account);
			var fileSystemOperations = new FileSystemOperations(log, new DefaultAzureCredential(), storageUri);
			var folderOperations = new FolderOperations(log, new DefaultAzureCredential(), storageUri, tlfp.FileSystem);

			// Create Folders and Assign permissions
			result = await folderOperations.CreateNewFolder(tlfp.Folder);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Folder Metadata
			result = await folderOperations.AddMetaData(tlfp.Folder, tlfp.FundCode, tlfp.FolderOwner);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Foler permissions
			if (tlfp.UserAccessList.Count == 0)
				tlfp.UserAccessList.Add(tlfp.FolderOwner);

			// Convert UserAccessList to Object Ids (both users and groups)
			var objectAccessList = await ConvertToObjectId(log, tlfp.UserAccessList);
			result = await folderOperations.AssignFullRwx(tlfp.Folder, objectAccessList);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Pull back details for display
			var folderDetail = folderOperations.GetFolderDetail(tlfp.Folder);

			return new OkObjectResult(folderDetail);
		}

		private static async Task<Dictionary<string, AccessControlType>> ConvertToObjectId(ILogger log, List<string> userAccessList)
		{
			var tokenCredential = new DefaultAzureCredential();
			var userOperations = new UserOperations(log, tokenCredential);
			var groupOperations = new GroupOperations(log, tokenCredential);

			var objectList = new Dictionary<string, AccessControlType>();
			foreach (var item in userAccessList)
			{
				var uid = await userOperations.GetObjectIdFromUPN(item);
				if (uid != null)
				{
					objectList.Add(uid, AccessControlType.User);
					continue;
				}

				var gid = await groupOperations.GetObjectIdFromGroupName(item);
				if (gid != null)
				{
					objectList.Add(gid, AccessControlType.Group);
					continue;
				}
			}
			return objectList;
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
