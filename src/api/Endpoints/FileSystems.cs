using Azure.Core;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.UsEduCsu.Saas
{
	public static class FileSystems
	{
		[FunctionName("FileSystemsByRbac")]
		public static IActionResult GetContainersByRbac(
			[HttpTrigger(AuthorizationLevel.Function, "GET", Route = "FileSystemsRbac")] HttpRequest req,
			ILogger log)
		{
			RoleOperations ro = new(log);
			var appCred = new DefaultAzureCredential();

			ClaimsPrincipalResult cpr = new ClaimsPrincipalResult(UserOperations.GetClaimsPrincipal(req));

			if (!cpr.IsValid) return new UnauthorizedResult();

			var principalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);
			// TODO: Foreach parallel (?) for subscriptions
			var SubscriptionId = SasConfiguration.ManagedSubscriptions;

			// TODO: See about getting them out in order?
			IList<RoleOperations.StorageDataPlaneRole> roleAssignments =
				ro.GetStorageDataPlaneRoles(SubscriptionId, principalId);

			// TODO: Unit test for pattern
			const string ScopePattern = @"^/subscriptions/[0-9a-f-]{36}/resourceGroups/[\w_\.-]{1,90}/providers/Microsoft.Storage/storageAccounts/(?<accountName>\w{3,24})(/blobServices/default/containers/(?<containerName>[\w-]{3,63}))?$";
			Regex re = new Regex(ScopePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

			IList<FileSystemResult> results = new List<FileSystemResult>();

			// Process the role assignments into storage accounts and container names
			foreach (var sdpr in roleAssignments)
			{
				// Determine if this is a storage account or container assignment
				// No support currently for higher-level assignments
				Match m = re.Match(sdpr.Scope);

				if (m.Success)
				{
					// There will always be a storage account name if there was a match
					string storageAccountName = m.Groups["accountName"].Value;

					// Find an existing entry for this storage account in the result set
					// Can't parallelize like this... but we shouldn't have to
					FileSystemResult fsr = results
						.SingleOrDefault(fsr => fsr.Name.Equals(storageAccountName, StringComparison.OrdinalIgnoreCase));

					// If this is a new FileSystemResult
					// (as opposed to retrieved from this function's return value)
					if (fsr == null)
					{
						// Set the storage account name property and add to result set
						fsr = new FileSystemResult() { Name = storageAccountName };
						results.Add(fsr);
					}

					// If there are potentially containers in this storage account
					// that aren't listed yet
					if (!fsr.AllFileSystems)
					{
						// Determine if this is a container-level assignment
						// that hasn't been added to the list of containers yet
						if (m.Groups["containerName"].Success
							&& !fsr.FileSystems.Contains(m.Groups["containerName"].Value))
						{
							// Assume access is only to this container
							fsr.FileSystems.Add(m.Groups["containerName"].Value);
						}
						else
						{
							var serviceUri = SasConfiguration.GetStorageUri(fsr.Name);

							// Access is to entire storage account; return all containers
							var adls = new FileSystemOperations(log, appCred, serviceUri);

							// Retrieve all the containers in the specified storage account
							var fileSystems = adls.GetFilesystems();
							// Override any prior added container names
							// I.e., completely replace the list of containers
							fsr.FileSystems = fileSystems.Select(fs => fs.Name).ToList();

							// There can't be any more containers in this storage account
							fsr.AllFileSystems = true;
						}
					}
				}
				else
				{
					// TODO: Log that scope format doesn't match expectation
				}
			}

			return new OkObjectResult(results);
		}

		[ProducesResponseType(typeof(FolderOperations.FolderDetail), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[FunctionName("FileSystemsContainer")]
		public static IActionResult GetContainer(
			[HttpTrigger(AuthorizationLevel.Function, "GET", Route = "FileSystems/{account}/{container}")]
			HttpRequest req,
			ILogger log, string account, string container)
		{
			if (!SasConfiguration.ValidateSharedKey(req, SasConfiguration.ApiKey.FileSystems))
			{
				return new UnauthorizedResult();
			}

			if (Services.Extensions.AnyNullOrEmpty(account, container))
			{
				return new BadRequestResult();
			}

			var ServiceUri = SasConfiguration.GetStorageUri(account);

			FolderOperations fo = new(ServiceUri, container, log);

			if (fo.FileSystemExists())
			{
				return new OkObjectResult(fo.GetFolderDetail(string.Empty));
			}
			else
			{
				return new NotFoundResult();
			}
		}

		[ProducesResponseType(typeof(FolderOperations.FolderDetail), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(List<FileSystemResult>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[FunctionName("FileSystems")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "POST", "GET", Route = "FileSystems/{account?}")]
			HttpRequest req,
			ILogger log, string account)
		{
			// TODO: Is the account param ever used?

			if (req.Method == HttpMethods.Post)
				return await FileSystemsPOST(req, log, account);
			else if (req.Method == HttpMethods.Get)
				return FileSystemsGET(req, log, account);

			// TODO: If this is even possible (accepted methods are defined above?),
			// return HTTP error code 405, response must include an Allow header with allowed methods
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
				{
					// TODO: Consider return HTTP 401 instead of HTTP 500
					// TODO: This is also a .NET Core 2.2 construct
					return new BadRequestErrorMessageResult("Call requires an authenticated user.");
				}
			}
			catch (Exception ex)
			{
				log.LogError(ex.Message);
				return new BadRequestErrorMessageResult("Unable to authenticate user.");
			}

			// TODO: principalId could be null
			var principalId = UserOperations.GetUserPrincipalId(claimsPrincipal);
			var userCred = CredentialHelper.GetUserCredentials(log, principalId);

			// Get the Containers for a upn from each storage account
			var accounts = SasConfiguration.GetConfiguration().StorageAccounts;
			if (account != null)
				accounts = accounts.Where(a => a.ToLowerInvariant() == account).ToArray();

			// Define the return value
			var result = new List<FileSystemResult>();

			RoleOperations roleOperations = new(log);

			var appCred = new DefaultAzureCredential();

			Parallel.ForEach(accounts, acct =>
			{
				IList<string> containers = null;

				try
				{
					// TODO: Consider renaming to GetPermissionedContainers
					containers = GetContainers(log, acct, principalId, appCred: appCred,
						userCred: userCred, roleOperations);
				}
				catch (Exception ex)
				{
					log.LogError(ex, "Error while retrieving containers for storage account '{acct}': '{Message}'.", acct, ex.Message);
					// Eat the exception here because otherwise a failure to read one account's
					// RBAC permissions wil cause the entire operation to fail
				}

				// If the current user has access to at least 1 container in the current storage account
				if (containers?.Count > 0)
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
		/// The userCred and the principal ID must refer to the same account.
		/// </summary>
		/// <param name="log"></param>
		/// <param name="account">The storage account for which to retrieve accessible containers.</param>
		/// <param name="principalId">The principal ID for which to retrieve accessible containers.</param>
		/// <param name="appCred">An access token for the app's identity.</param>
		/// <param name="userCred">An access token to impersonate the calling user (same as principalId) when calling the Storage API.</param>
		/// <returns>The list of containers to which the specified principal has access.</returns>
		private static IList<string> GetContainers(ILogger log, string account, string principalId,
			TokenCredential appCred, TokenCredential userCred,
			RoleOperations roleOps)
		{
			// TODO: validate that userCred and principalId match?

			// Define the return value (never return null)
			var accessibleContainers = new List<string>();

			var serviceUri = SasConfiguration.GetStorageUri(account);
			var adls = new FileSystemOperations(log, appCred, serviceUri);

			// Retrieve all the containers in the specified storage account
			var fileSystems = adls.GetFilesystems();

			// Check for RBAC data plane access to any container in the account
			IList<RoleOperations.ContainerRole> containerDataPlaneRoleAssignments = null;

			try
			{
				containerDataPlaneRoleAssignments = roleOps
								.GetContainerRoleAssignments(account, principalId);
			}
			catch (Exception ex)
			{
				log.LogError(ex, "Unable to retrieve container RBAC assignments for '{account}'", account);
				throw;
			}

			// Join fileSystems and roleAssignments due to orphaned role assignments
			var fileSystemRoleAssignments = fileSystems.Join(containerDataPlaneRoleAssignments,
					fs => fs.Name,
					ra => ra.Container,
					(_, ra) => ra)
					.ToList();

			// If the specified principal has any data plane RBAC assignment on any container
			if (fileSystemRoleAssignments.Count > 0)
			{
				// They have access to these containers
				fileSystemRoleAssignments.ForEach(r => accessibleContainers.Add(r.Container));
			}

			// For any containers where the principal doesn't have a data plane RBAC role
			Parallel.ForEach(fileSystems.Where(fs => !accessibleContainers.Any(c => c == fs.Name)), filesystem =>
			{
				// Evaluate top-level folder ACLs, check if user can read folders
				var folderOps = new FolderOperations(serviceUri, filesystem.Name, log, appCred);
				// Retrieve all folders in the container
				var folderList = folderOps.GetFolderList();

				// Check for any folder using calling user's credentials
				var folderOpsAsUser = new FolderOperations(serviceUri, filesystem.Name, log, userCred, principalId);
				var folders = folderOpsAsUser.GetAccessibleFolders(folderList, checkForAny: true);

				if (folders.Count > 0)
					accessibleContainers.Add(filesystem.Name);
			});

			return accessibleContainers;
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
				return new BadRequestErrorMessageResult($"{nameof(FileSystemParameters)} is missing or malformed. Review the API documentation and check the expected body contents.");

			// Add Route Parameters, if needed
			tlfp.StorageAcount ??= account;

			// Check Parameters
			string error = null;
			if (Services.Extensions.AnyNull(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode, tlfp.StorageAcount))
				error = $"{nameof(FileSystemParameters)} is malformed. Review the API documentation and check the expected body contents.";
			else if (tlfp.Owner.Contains("#EXT#"))
				error = "Guest accounts are not supported as container owners.";

			if (error != null)
				return new BadRequestErrorMessageResult(error);

			// Setup Azure Credential
			var tokenCredential = new DefaultAzureCredential();

			// Get the new container's Owner
			var userOperations = new UserOperations(log, tokenCredential);

			string ownerObjectId;

			// If the Owner value doesn't look like a GUID (the value has already been checked for null or empty)
			if (!Guid.TryParse(tlfp.Owner, out Guid tested))
			{
				// Assume it's a UPN and translate it to the AAD object ID
				// TODO: Why could it not be a group? (Might even recommend it to be a group?)
				ownerObjectId = await userOperations.GetObjectIdFromUPN(tlfp.Owner);

				if (string.IsNullOrEmpty(ownerObjectId))
					return new BadRequestErrorMessageResult($"Owner identity not found in AAD. Please verify that '{tlfp.Owner}' is a valid member UPN or object ID and that the application has 'User.Read.All' permission in the directory.");
			}
			else
			{
				// Assume it's an AAD object ID
				ownerObjectId = tlfp.Owner;
			}

			// Call each of the steps in order and error out if anytyhing fails
			var storageUri = SasConfiguration.GetStorageUri(tlfp.StorageAcount);
			var fileSystemOperations = new FileSystemOperations(log, tokenCredential, storageUri);

			// Create File System
			Result result = await fileSystemOperations.CreateFileSystem(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

			// Assign Other Execute Permission
			result = await fileSystemOperations.SetRootOtherACL(tlfp.FileSystem);
			if (!result.Success)
				return new BadRequestErrorMessageResult($"Error setting root ACL: {result.Message}");

			// Add Blob Owner
			var roleOperations = new RoleOperations(log);
			result = roleOperations.AssignRoles(tlfp.StorageAcount, tlfp.FileSystem, ownerObjectId);
			if (!result.Success)
				return new BadRequestErrorMessageResult($"Error assigning RBAC role: {result.Message}");

			// Get the new container's root folder's details
			var folderOperations = new FolderOperations(storageUri, tlfp.FileSystem, log, tokenCredential);
			var folderDetail = folderOperations.GetFolderDetail(string.Empty);

			if (folderDetail is null)
				return new BadRequestErrorMessageResult("File system creation succeeded, but unable to retrieve new container root folder details.");

			return new OkObjectResult(folderDetail) { StatusCode = StatusCodes.Status201Created };
		}

		private static async Task<FileSystemParameters> GetFileSystemParameters(HttpRequest req, ILogger log)
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

			try
			{
				return JsonSerializer.Deserialize<FileSystemParameters>(body);
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				return null;
			}
		}

		private class FileSystemResult
		{
			public string Name { get; set; }

			public List<string> FileSystems { get; internal set; } = new List<string>();

			public bool AllFileSystems { get; set; }
		}

		internal class FileSystemParameters
		{
			public string StorageAcount { get; set; }

			public string FileSystem { get; set; }

			public string FundCode { get; set; }

			public string Owner { get; set; }
		}

		internal class ClaimsPrincipalResult
		{
			// TODO: Refactor UserOperations to return this instead of a ClaimsPrincipal
			// Move some of this logic into UserOperations.GetClaimsPrincipal

			/// <summary>
			/// Constructs a potentially valid ClaimsPrincipalResult using the specified ClaimsPrincipal.
			/// </summary>
			/// <param name="cp"></param>
			/// <exception cref="ArgumentNullException"></exception>
			public ClaimsPrincipalResult(ClaimsPrincipal cp)
			{
				if (!Services.Extensions.AnyNull(cp, cp.Identity))
				{
					ClaimsPrincipal = cp;
					// IsValid is false by default, only need to set if it's a valid principal
					IsValid = true;
				}
				else
				{
					ClaimsPrincipal = null;
					Message = "Call requires an authenticated user.";
				}
			}

			/// <summary>
			/// Constructs an invalid ClaimsPrincipalResult using the specified error message.
			/// </summary>
			/// <param name="errorMessage"></param>
			/// <exception cref="ArgumentNullException"></exception>
			public ClaimsPrincipalResult(string errorMessage)
			{
				if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentNullException(nameof(errorMessage));

				ClaimsPrincipal = null;
				Message = errorMessage;
			}

			public bool IsValid { get; set; }
			public string Message { get; set; }
			public ClaimsPrincipal ClaimsPrincipal { get; private set; }
			// TODO: Add UserPrincipalId property as a shortcut
		}
	}
}
