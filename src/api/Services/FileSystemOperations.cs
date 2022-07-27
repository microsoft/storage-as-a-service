using Azure.Core;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas.Services
{
	public class FileSystemOperations
	{
		private readonly ILogger log;
		private readonly DataLakeServiceClient dlsClient;

		public FileSystemOperations(ILogger log, TokenCredential tokenCredential, Uri storageUri)
		{
			this.log = log;
			dlsClient = new DataLakeServiceClient(storageUri, tokenCredential);
		}

		public async Task<Result> SetRootOtherACL(string fileSystem)
		{
			var result = new Result();
			try
			{
				// Get Root Directory Client
				var directoryClient = dlsClient.GetFileSystemClient(fileSystem).GetDirectoryClient(string.Empty);

				// Retrieve the current ACL on the root directory
				var acl = (await directoryClient.GetAccessControlAsync(userPrincipalName: true)).Value.AccessControlList.ToList();

				// Don't need to add "Other" because it's implicit
				// Find the "Other" entry in the ACL
				acl.Single(a => a.AccessControlType == AccessControlType.Other)
					// Set permissions for "Other" to --X
					.Permissions = RolePermissions.Execute;

				// Update root container's ACL
				var response = directoryClient.SetAccessControlList(acl).GetRawResponse();
				result.Success = true;
				return result;
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				result.Message = ex.Message;
				return result;
			}
		}

		internal IEnumerable<FileSystemItem> GetContainers()
		{
			List<FileSystemItem> fileSystems;

			try
			{
				fileSystems = dlsClient.GetFileSystems().ToList();
			}
			catch (Exception ex)
			{
				fileSystems = new List<FileSystemItem>();
				log.LogError(ex, ex.Message);
			}

			return fileSystems;
		}

		[Obsolete("Use GetContainers instead.")]
		public IEnumerable<FileSystemItem> GetFilesystems()
		{
			return GetContainers();
		}

		public async Task<Result> CreateFileSystem(string fileSystemName, string owner, string fundCode)
		{
			var result = new Result();

			// Check to see if File System already exists
			try
			{
				var fileSystem = dlsClient.GetFileSystems(prefix: fileSystemName)
					.FirstOrDefault(p => p.Name == fileSystemName);

				if (fileSystem != null)
					return new Result() { Message = $"A file system '{fileSystemName}' already exists in '{dlsClient.AccountName}'.", Success = true };
			}
			catch (Exception ex)
			{
				log.LogError(ex, "Error while trying to query for the existence of container '{fileSystemName}' in account '{dlsClientAccountName}': '{exMessage}'.", fileSystemName, dlsClient.AccountName, ex.Message);
				result.Success = false;
				result.Message = $"Error while trying to query for the existence of container '{fileSystemName}' in account '{dlsClient.AccountName}': '{ex.Message}'."; ;
			}

			// Prepare metadata
			var metadata = new Dictionary<string, string>
			{
				{ "FundCode", fundCode },
				{ "Owner", owner }
			};

			// Create the new File System
			try
			{
				var dlFileSystemResponse = await dlsClient.CreateFileSystemAsync(fileSystemName, PublicAccessType.None, metadata);
				result.Success = true;
			}
			catch (Exception ex)
			{
				log.LogError(ex, "Error while creating new container '{fileSystemName}' in account '{dlsClientAccountName}': '{exMessage}'.", fileSystemName, dlsClient.AccountName, ex.Message);
				result.Success = false;
				result.Message = $"Error while creating new container '{fileSystemName}' in account '{dlsClient.AccountName}': '{ex.Message}'.";
			}

			return result;
		}
	}
}
