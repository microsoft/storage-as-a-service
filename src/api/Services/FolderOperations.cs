using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class FolderOperations
	{
		private readonly ILogger log;
		private readonly DataLakeFileSystemClient dlfsClient;
		private readonly decimal costPerTB;
		private readonly TokenCredential tokenCredential;
		public FolderOperations(ILogger log, TokenCredential tokenCredential, Uri storageUri, string fileSystem)
		{
			this.log = log;
			this.tokenCredential = tokenCredential;
			var costPerTB = Environment.GetEnvironmentVariable("COST_PER_TB");
			if (costPerTB != null)
				decimal.TryParse(costPerTB, out this.costPerTB);

			// TODO: Call helper function to create DataLakeServiceClient
			dlfsClient = new DataLakeServiceClient(storageUri, tokenCredential).GetFileSystemClient(fileSystem);
		}

		internal async Task<Result> CreateNewFolder(string folder)
		{
			var result = new Result();
			log.LogTrace($"Creating the folder '{folder}' within the container '{dlfsClient.Uri}'...");

			try
			{
				var directoryClient = dlfsClient.GetDirectoryClient(folder);
				var response = await directoryClient.CreateIfNotExistsAsync();	// Returns null if exists
				result.Success = response?.GetRawResponse().Status == 201;

				if (!result.Success)
				{
					if (response == null)
						result.Message = "Folder already exists";
					log.LogError(result.Message);
				}
			}
			catch (Exception ex)
			{
				result.Message = ex.Message;
				log.LogError(result.Message);
			}

			return result;
		}

		internal async Task<Result> AssignFullRwx(string folder, string folderOwner)
		{
			log.LogTrace($"Assigning RWX permission to Folder Owner ({folderOwner}) at folder's ({folder}) level...");

			var result = new Result();
			var directoryClient = dlfsClient.GetDirectoryClient(folder);
			var accessControlListUpdate = new List<PathAccessControlItem>
			{
				new PathAccessControlItem(
					accessControlType: AccessControlType.User,
					permissions: RolePermissions.Read | RolePermissions.Write | RolePermissions.Execute,
					entityId: folderOwner),
				new PathAccessControlItem(
					accessControlType: AccessControlType.User,
					permissions: RolePermissions.Read | RolePermissions.Write | RolePermissions.Execute,
					entityId: folderOwner,
					defaultScope: true)
			};

			// Send up changes
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
			log.LogTrace($"Calculating size for ({dlfsClient.Uri})/({folder})");

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

		private static string Simplify(string s)
		{
			if (string.IsNullOrEmpty(s)) return null;

			return s.Replace('@', '_').ToLower();
		}

		internal IList<FolderDetail> GetAccessibleFolders(string upn, string principalId)
		{
			var accessibleFolders = new List<FolderDetail>();
			try
			{
				// Get Role Assignments
				var roleOperations = new RoleOperations(log, tokenCredential);

				// Translate for guest accounts
				var guestUpn = Simplify(upn);

				// Get Root Folder
				bool principalIsOwner = false;
				var fd = GetFolderDetail(string.Empty);
				if (fd != null)
				{
					var roles = roleOperations.GetContainerRoleAssignments(dlfsClient.AccountName, principalId)
											.Where( ra => ra.Container == dlfsClient.Name
														&& ra.PrincipalId == principalId);
					if (roles.Any()) {
						fd.Name = "{root}";
						fd.UserAccess = new List<string>(roles.Select( ra => $"{ra.RoleName}: {upn}"));
						accessibleFolders.Add(fd);

						principalIsOwner = fd.UserAccess.Any(ra => ra.Contains("Owner"));
					}
				}

				// Get all Top Level Folders
				var flds = dlfsClient.GetPaths().ToList();
				var folders = flds.Where<PathItem>(
					pi => pi.IsDirectory != null && (bool)pi.IsDirectory)
					.ToList();

				// Find folders that have ACL entries for upn
				Parallel.ForEach(folders, folder =>
					{
						var fd = GetFolderDetail(folder.Name);
						if (fd != null && (principalIsOwner || fd.UserAccess.Any( u => Simplify(u).StartsWith(guestUpn))))
							accessibleFolders.Add(fd);
					}
				);
			}
			catch (Exception ex)
			{
				log.LogError(ex.Message);
			}
			return accessibleFolders;
		}

		internal FolderDetail GetFolderDetail(string folderName)
		{
			try
			{
				var isRoot = string.IsNullOrEmpty(folderName);

				var rootClient = dlfsClient.GetDirectoryClient(folderName);  // container (root)
				var acl = rootClient.GetAccessControl(userPrincipalName: true).Value.AccessControlList;
				string createdOn = string.Empty, accessTier = string.Empty;
				IDictionary<string, string> metadata;
				if (isRoot)
					metadata = dlfsClient.GetProperties().Value.Metadata;
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
			catch (Exception ex)
			{
				log.LogError(ex.Message, ex);
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
				.Where(p => p.AccessControlType == AccessControlType.User
							&& p.EntityId != null && !p.DefaultScope
							&& p.Permissions.HasFlag(RolePermissions.Read))
				.Select(p => p.EntityId)
				.ToList();

			// Create Folder Details
			var fd = new FolderDetail()
			{
				Name = folder,
				Size = size.HasValue ? size.Value.ToString() : null,
				Cost = cost.HasValue ? cost.Value.ToString() : null,
				FundCode = metadata.ContainsKey("FundCode") ? metadata["FundCode"] : null,
				UserAccess = userAccess,
				URI = uri.ToString(),
				Owner = metadata.ContainsKey("Owner") ? metadata["Owner"] : null,
			};
			return fd;
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
			public string Region { get; set; }
			public string URI { get; set; }
			public IList<string> UserAccess { get; set; }
		}
	}
}
