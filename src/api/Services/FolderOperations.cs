using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class FolderOperations
	{
		private readonly ILogger log;
		private readonly DataLakeFileSystemClient dlfsClient;
		private readonly decimal costPerTB;
		private readonly string principalId;

		/// <summary>
		/// Initializes a new instance of the FolderOperations class.
		/// </summary>
		/// <param name="storageUri"></param>
		/// <param name="fileSystem"></param>
		/// <param name="log"></param>
		/// <param name="tokenCredential"></param>
		/// <param name="principalId"></param>
		public FolderOperations(Uri storageUri, string fileSystem, ILogger log,
			TokenCredential tokenCredential, string principalId)
		{
			ArgumentNullException.ThrowIfNull(tokenCredential, nameof(tokenCredential));

			this.log = log;
			this.principalId = principalId;

			var costPerTB = Environment.GetEnvironmentVariable("COST_PER_TB");
			if (!string.IsNullOrWhiteSpace(costPerTB))
				_ = decimal.TryParse(costPerTB, out this.costPerTB);

			// TODO: Avoid creating a new DataLakeServiceClient every time
			// (FolderOperations objects are created in loops)
			// Possible solution: store tokenCredential and associated client in a static dictionary?
			dlfsClient = new DataLakeServiceClient(storageUri, tokenCredential)
				.GetFileSystemClient(fileSystem);
		}

		internal async Task<Result> CreateNewFolder(string folder)
		{
			var result = new Result();
			log.LogTrace("Creating the folder '{folder}' within the container '{dlfsClientUri}'.",
				folder, dlfsClient.Uri);

			try
			{
				var directoryClient = dlfsClient.GetDirectoryClient(folder);
				var response = await directoryClient.CreateIfNotExistsAsync();  // Returns null if exists
				result.Success = response?.GetRawResponse().Status == 201;

				if (!result.Success
					&& response == null)
				{
					log.LogError("Folder '{dlfsClientAccountName}/{dlfsClientName}/{folder}' already exists.",
						dlfsClient.AccountName, dlfsClient.Name, folder);
					result.Message = $"Folder '{dlfsClient.AccountName}/{dlfsClient.Name}/{folder}' already exists.";
				}
			}
			catch (Exception ex)
			{
				result.Message = ex.Message;
				log.LogError(ex, result.Message);
			}

			return result;
		}

		internal async Task<Result> AssignFullRwx(string folder, Dictionary<string, AccessControlType> userAccessList)
		{
			var ual = string.Join(", ", userAccessList);
			log.LogTrace("Assigning RWX permission to Folder Owner ({ual}) at folder's ({folder}) level...", ual, folder);

			var accessControlListUpdate = new List<PathAccessControlItem>();
			foreach (var user in userAccessList)
			{
				var access = new PathAccessControlItem(
					accessControlType: user.Value,
					permissions: RolePermissions.Read | RolePermissions.Write | RolePermissions.Execute,
					entityId: user.Key);
				var defaultAccess = new PathAccessControlItem(
					accessControlType: user.Value,
					permissions: RolePermissions.Read | RolePermissions.Write | RolePermissions.Execute,
					entityId: user.Key,
					defaultScope: true);
				accessControlListUpdate.Add(access);
				accessControlListUpdate.Add(defaultAccess);
			}

			// Send up changes
			var result = new Result();
			var directoryClient = dlfsClient.GetDirectoryClient(folder);
			var resultACL = await directoryClient.UpdateAccessControlRecursiveAsync(accessControlListUpdate);
			result.Success = resultACL.GetRawResponse().Status == (int)HttpStatusCode.OK;
			result.Message = result.Success ? null : "Error trying to assign the RWX permission to the folder. Error 500.";
			return result;
		}

		internal async Task<Result> AddMetaData(string folder, string fundCode, string owner)
		{
			log.LogTrace($"Saving FundCode into container's metadata...");
			try
			{
				var directoryClient = dlfsClient.GetDirectoryClient(folder);

				// Check the Last Calculated Date from the Metadata
				var meta = (await directoryClient.GetPropertiesAsync()).Value.Metadata;

				// Add Fund Code
				meta.Add("FundCode", fundCode);
				meta.Add("Owner", owner);

				// Strip off a readonly item
				meta.Remove("hdi_isfolder");

				// Save back into the Directory Metadata
				directoryClient.SetMetadata(meta);
			}
			catch (Exception ex)
			{
				return new Result { Success = false, Message = ex.Message };
			}

			return new Result() { Success = true };
		}

		internal async Task<long> CalculateFolderSize(string folder)
		{
			const string sizeCalcDateKey = "SizeCalcDate";
			const string sizeKey = "Size";
			log.LogTrace("Calculating size for ({dlfsClientUri})/({folder})", dlfsClient.Uri, folder);

			var directoryClient = dlfsClient.GetDirectoryClient(folder);

			// Check the Last Calculated Date from the Metadata
			var meta = (await directoryClient.GetPropertiesAsync()).Value.Metadata;
			var sizeCalcDate = meta.ContainsKey(sizeCalcDateKey)
				? DateTime.Parse(meta[sizeCalcDateKey])
				: DateTime.MinValue;

			// If old calculate size again
			if (DateTime.UtcNow.Subtract(sizeCalcDate).TotalDays > 7)
			{
				var paths = directoryClient.GetPaths(true, false);
				long size = 0;
				foreach (var path in paths)
				{
					size += (path.ContentLength.HasValue) ? (int)path.ContentLength : 0;
				}
				meta[sizeCalcDateKey] = DateTime.UtcNow.ToString();
				meta[sizeKey] = size.ToString();

				// Strip off a readonly item
				meta.Remove("hdi_isfolder");

				// Save back into the Directory Metadata
				directoryClient.SetMetadata(meta);
			}

			return long.Parse(meta[sizeKey]);
		}

		/// <summary>
		/// Returns the complete list of folders for the current container.
		/// </summary>
		/// <returns>An IList<PathItem> of all folders in the current container.</PathItem></returns>
		internal IList<PathItem> GetFolderList()
		{
			List<PathItem> folders = null;

			try
			{
				// Get all Top Level Folders, using the app identity
				// They will be filtered later when using the user credentials to get folder details
				folders = dlfsClient.GetPaths()
					.Where(pi => pi.IsDirectory == true)
					.ToList();
			}
			catch (Exception ex)
			{
				log.LogError(ex, "{dlfsClientAccountName}/{dlfsClientName} {exMessage}",
					dlfsClient.AccountName, dlfsClient.Name, ex.Message);
			}

			return folders;
		}

		/// <summary>
		/// Returns a (partial) list of top-level folders that the principal's whose token is specified can access.
		/// </summary>
		/// <param name="checkForAny">If set to true, stops enumerating folders
		/// when the first permissioned folder is found.</param>
		/// <returns>A list of top-level folders the principal represented by the current token has access to.
		/// If checkForAny is true, the list is only a partial list.</returns>
		internal IList<FolderDetail> GetAccessibleFolders(IList<PathItem> folders, bool checkForAny = false)
		{
			// Define the return value
			var accessibleFolders = new ObservableCollection<FolderDetail>();

			var cancelSource = new CancellationTokenSource();
			var po = new ParallelOptions()
			{
				CancellationToken = cancelSource.Token
			};

			// Prepare a handler to stop the enumeration of folders as soon as 1 is found
			// Only create the handler if checkForAny == true
			if (checkForAny)
			{
				accessibleFolders.CollectionChanged += (s, e) =>
				{
					if (checkForAny)
						cancelSource.Cancel();
				};
			}

			try
			{
				Parallel.ForEach(folders, po, folder =>
					{
						if (po.CancellationToken.IsCancellationRequested)
							return;

						// Attempt to retrieve the folder's detail
						var fd = GetFolderDetail(folder.Name);

						if (fd != null)
							accessibleFolders.Add(fd);
					}
				);
			}
			catch (OperationCanceledException ex)
			{
				// TODO: Is this log entry worth the overhead?
				log.LogTrace(ex, "Parallel op cancelled.");
			}
			finally
			{
				cancelSource.Dispose();
			}

			return accessibleFolders;
		}

		/// <summary>
		/// Retrieves a FolderDetail object for the specified folder in the current file system.
		/// </summary>
		/// <param name="folderName">The name of the folder.</param>
		/// <returns>A FolderDetail instance with the metadata of the folder, or null if the folder is not accessible by the current identity.</returns>
		internal FolderDetail GetFolderDetail(string folderName)
		{
			try
			{
				var isRoot = string.IsNullOrEmpty(folderName);

				var rootClient = dlfsClient.GetDirectoryClient(folderName);  // container (root)
				var acl = rootClient.GetAccessControl(userPrincipalName: true).Value.AccessControlList;
				string createdOn = string.Empty, accessTier = string.Empty;
				IDictionary<string, string> metadata = new Dictionary<string, string>();

				if (isRoot)
				{
					metadata = dlfsClient.GetProperties().Value.Metadata;
				}
				else
				{
					var prop = rootClient.GetProperties().Value;
					metadata = prop.Metadata;
					createdOn = prop.CreatedOn.ToLocalTime().ToString(); // TODO: LocalTime probably doesn't mean anything when run in an Azure Fx
					accessTier = prop.AccessTier;
				}

				FolderDetail fd = BuildFolderDetail(folderName, metadata, acl, rootClient.Uri);

				if (!isRoot)
				{
					fd.CreatedOn = createdOn;
					fd.AccessTier = accessTier;
				}

				return fd;
			}
			catch (AuthenticationFailedException ex)
			{
				// We should expect there to be plenty of authentication exceptions
				// Trace instead of log as exception
				log.LogTrace(ex, "Authentication to '{AccountName}/{Container}/{Folder}' failed for principal '{principalId}'. Exception Message: '{Message}'.",
					dlfsClient.AccountName, dlfsClient.Name, folderName, principalId, ex.Message);
			}
			catch (Exception ex)
			{
				log.LogError(ex, "Error while retrieving folder details for '{AccountName}/{Container}/{Folder}' with principal '{principalId}'. Exception Message: '{Message}'.",
					dlfsClient.AccountName, dlfsClient.Name, folderName, principalId, ex.Message);
				// The callers expect null, no exception handling in callers ==> eat the exception
			}

			return null;
		}

		private FolderDetail BuildFolderDetail(
			string folder, IDictionary<string, string> metadata,
			IEnumerable<PathAccessControlItem> acl, Uri uri)
		{
			// Get Metadata
			long? size = metadata.ContainsKey("Size") ? long.Parse(metadata["Size"]) : null;
			decimal? cost = (size == null) ? null : size * costPerTB / 1000000000000;

			// Calculate UserAccess
			var userAccess = acl
				.Where(p => (p.AccessControlType == AccessControlType.User
							|| p.AccessControlType == AccessControlType.Group)
							&& p.EntityId != null && !p.DefaultScope
							&& p.Permissions.HasFlag(RolePermissions.Read))
				.Select(p => p.EntityId)
				.ToList();

			TranslateGroups(userAccess); // TODO: Opportunity for caching here

			var seFolderName = HttpUtility.UrlEncode(folder.Length > 0 ? folder + "/" : string.Empty);
			var seEndpoint = HttpUtility.UrlEncode(dlfsClient.Uri.ToString()); // TODO: Opportunity for efficiency

			// Create Folder Details
			var fd = new FolderDetail()
			{
				Name = folder,
				Size = size.HasValue ? size.Value.ToString() : "NA",
				Cost = cost.HasValue ? cost.Value.ToString() : "NA",
				FundCode = metadata.ContainsKey("FundCode") ? metadata["FundCode"] : null,
				UserAccess = userAccess,
				// Uri.ToString() always appends a trailing /, remove it (#82)
				URI = uri.ToString().TrimEnd('/'),
				Owner = metadata.ContainsKey("Owner") ? metadata["Owner"] : null,
				StorageExplorerURI = $"storageexplorer://?v=2&tenantId={SasConfiguration.TenantId}&type=fileSystemPath&container={dlfsClient.Name}&serviceEndpoint={seEndpoint}&path={seFolderName}"
			};

			return fd;
		}

		/// <summary>
		/// Translates AAD group Object IDs into group names in the specified user access list.
		/// The original user access list is modified: group object IDs are replaced by group names.
		/// </summary>
		/// <param name="userAccess">The list of access control entries.</param>
		private void TranslateGroups(IList<string> userAccess)
		{
			var groupOperations = new GroupOperations(log, new DefaultAzureCredential());

			for (int i = 0; i < userAccess.Count(); i++)
			{
				var groupObjectId = userAccess[i];

				// Check if the current user access entry is a GUID
				if (Guid.TryParse(groupObjectId, out Guid guid))
				{
					// Assume it's an AAD group object ID and retrieve the group name
					var groupName = groupOperations.GetGroupNameFromObjectId(groupObjectId).Result;

					// Replace the current list item, if it is a group name, otherwise, leave the GUID in place
					userAccess[i] = !string.IsNullOrEmpty(groupName) ? groupName : userAccess[i];
				}
			}
		}

		internal class FolderDetail
		{
			public string Name { get; set; }
			public string CreatedOn { get; set; }
			public string AccessTier { get; set; }
			public string Size { get; set; }
			public string Cost { get; set; }
			public string FundCode { get; set; }
			public string Owner { get; set; }
			public string Region { get => "Not Implemented"; }
			public string URI { get; set; }
			public IList<string> UserAccess { get; set; }
			public string StorageExplorerURI { get; set; }
		}
	}
}
