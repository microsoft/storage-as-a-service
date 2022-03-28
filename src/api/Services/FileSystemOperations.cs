using Azure.Core;
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
				var acl = (await directoryClient.GetAccessControlAsync(userPrincipalName: true)).Value.AccessControlList.ToList();

				// Add Other Entry
				acl.Add(new PathAccessControlItem(AccessControlType.Other,
					RolePermissions.Execute, false));

				// Update root container's ACL
				var response = directoryClient.SetAccessControlList(acl).GetRawResponse();
				result.Success = response.Status == ((int)HttpStatusCode.OK);
				result.Message = result.Success ? null : $"Error adding Other as Execute on the root folder of container '{fileSystem}'. Error {response.Status}.";
				return result;
			}
			catch (Exception ex)
			{
				result.Message = ex.Message;
				return result;
			}
		}

		public IEnumerable<FileSystemItem> GetFilesystems()
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

		public async Task<Result> CreateFileSystem(string fileSystemName, string owner, string fundCode)
		{
			// Check to see if File System already exists
			var fileSystem = dlsClient.GetFileSystems(prefix: fileSystemName)
				.FirstOrDefault(p => p.Name == fileSystemName);
			if (fileSystem != null)
				return new Result() { Message = $"A file system '{fileSystemName}' already exists in '{dlsClient.AccountName}'.", Success = true };

			// Prepare metadata
			var metadata = new Dictionary<string, string>
			{
				{ "FundCode", fundCode },
				{ "Owner", owner }
			};

			// Create the new File System
			var result = new Result();
			try
			{
				var dlFileSystemResponse = await dlsClient.CreateFileSystemAsync(fileSystemName, PublicAccessType.None, metadata);
				result.Success = true;
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Message = ex.Message;
			}

			return result;
		}
	}
}
