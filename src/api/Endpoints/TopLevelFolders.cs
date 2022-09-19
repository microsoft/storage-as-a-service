// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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
		public static IActionResult TopLevelFoldersGET(
			[HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "TopLevelFolders/{account}/{filesystem}/{user?}")]
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
			var appCred = new DefaultAzureCredential();

			// Get User Credentials
			var userCred = CredentialHelper.GetUserCredentials(log, principalId);
			var folderOperations = new FolderOperations(storageUri, filesystem, log, appCred);

			// Retrieve all folders in the container
			var folderList = folderOperations.GetFolderList();

			// Filter for ALL the top-level folders in the container that are accessible by the user
			var folderOperationsAsUser = new FolderOperations(storageUri, filesystem, log,
				userCred, principalId);

			var folders = folderOperationsAsUser.GetAccessibleFolders(folderList);

			// Retrieve the container's data plane RBAC role assignments for the calling user
			// TODO: Possible improvement: if the calling user is the owner per RBAC (or any RBAC data plane role?),
			// simply retrieve all folders instead of checking each folder (done in the the call above)?
			var roleOperations = new RoleOperations(log);
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
				[HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "TopLevelFolders/{account}/{filesystem}")]
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

			// Check Parameters
			string error = null;
			if (Services.Extensions.AnyNullOrEmpty(tlfp.FileSystem, tlfp.Folder, tlfp.FolderOwner, tlfp.FundCode, tlfp.StorageAcount))
			{
				error = $"{nameof(TopLevelFolderParameters)} is malformed.";
				return new BadRequestErrorMessageResult(error);
			}

			// Authorize the calling user as owner of the container
			var roleOperations = new RoleOperations(log);
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

			// Call each of the steps in order and error out if anything fails
			Result result = null;
			var storageUri = SasConfiguration.GetStorageUri(account);
			TokenCredential ApiCredential = new DefaultAzureCredential();
			var folderOperations = new FolderOperations(storageUri, tlfp.FileSystem, log, ApiCredential);

			// Create the new folder
			result = await folderOperations.CreateNewFolder(tlfp.Folder);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Initialize the API response object
			var retval = new FolderCreateResult();

			// Assign the folder's metadata
			result = await folderOperations.AddMetaData(tlfp.Folder, tlfp.FundCode, tlfp.FolderOwner);
			if (!result.Success)
			{
				log.LogError("Error while setting metadata for new folder '{account}/{container}/{folder}': '{resultMessage}'",
					account, filesystem, tlfp.Folder, result.Message);
				// At this point, the folder has been created.
				// ==> Don't return error, but add message to return value
				retval.Message = result.Message;
			}

			// If an ACL for the new folder is specified
			if (tlfp.UserAccessList != null
				&& tlfp.UserAccessList.Count > 0)
			{

				try
				{
					// Convert UserAccessList to Object Ids (both users and groups)
					var objectAccessList = await ConvertToObjectId(log, tlfp.UserAccessList);

					if (objectAccessList.Count > 0)
					{
						// Assign RWX ACL to each object ID
						result = await folderOperations.AssignFullRwx(tlfp.Folder, objectAccessList);

						if (!result.Success)
						{
							log.LogError("Error while assigning ACLs to new folder '{account}/{container}/{folder}': '{resultMessage}'",
								account, filesystem, tlfp.Folder, result.Message);
							// At this point, the folder has been created.
							// ==> Don't return error, but add message to return value
							retval.Message = result.Message;
						}
					}
				}
				catch (Exception ex)
				{
					log.LogError(ex, "Exception while translating or assigning ACLs to new folder '{account}/{container}/{folder}'.",
						account, filesystem, tlfp.Folder);
					// TODO: At this point, the folder has been created.
					// ==> Don't return error, but add message to value
					retval.Message = "Folder created, but unable to assign ACL to the specified user list.";
				}
			}
			else
			{
				retval.Message = "No ACL assigned. Only root folder RBAC permissions will be inherited.";
			}

			// Pull back details for display
			retval.FolderDetail = folderOperations.GetFolderDetail(tlfp.Folder);

			return new OkObjectResult(retval) { StatusCode = StatusCodes.Status201Created };
		}

		/// <summary>
		/// Converts AAD UPNs and group names into object IDs.
		/// </summary>
		/// <param name="log"></param>
		/// <param name="userAccessList"></param>
		/// <returns></returns>
		private static async Task<Dictionary<string, AccessControlType>> ConvertToObjectId(ILogger log, List<string> userAccessList)
		{
			var tokenCredential = new DefaultAzureCredential();
			var userOperations = new UserOperations(log, tokenCredential);
			var graphOperations = new MicrosoftGraphOperations(log, tokenCredential);

			// TODO: Use ConcurrentDictionary for thread-safety
			var objectList = new Dictionary<string, AccessControlType>();

			ParallelOptions pOptions = new();

			await Parallel.ForEachAsync(userAccessList, pOptions, async (item, cToken) =>
			{
				if (cToken.IsCancellationRequested
					|| string.IsNullOrEmpty(item))
				{
					return;
				}

				// If this access control entry is a UPN
				if (IsUpn(item))
				{
					// Translate the UPN into a principal ID
					var uid = graphOperations.GetObjectIdFromUPN(item);
					if (uid != null)
						objectList.Add(uid, AccessControlType.User);
				}
				else
				{
					// Assume it's a group name; translate inot a group Object ID
					var gid = graphOperations.GetObjectIdFromGroupName(item);
					if (gid != null)
						objectList.Add(gid, AccessControlType.Group);
				}

				// TODO: Consider keeping track of which ACEs could not be translated and reporting back to user
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

		internal class FolderCreateResult
		{
			public FolderOperations.FolderDetail FolderDetail { get; set; }
			public string Message { get; set; }
		}
	}
}
