using Azure.Core;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		//public async Task<Result> AddsFolderOwnerToContainerACLAsExecute(string fileSystem, string folderOwner)
		//{
		//	var targetPermissions = RolePermissions.Execute | RolePermissions.Read;

		//	var result = new Result();
		//	log.LogTrace($"Adding '{folderOwner}' (Folder Owner) to the container '{dlsClient}/{fileSystem}' as 'Execute'...");

		//	// Get Root Directory Client
		//	var directoryClient = dlsClient.GetFileSystemClient(fileSystem).GetDirectoryClient(string.Empty);
		//	var acl = (await directoryClient.GetAccessControlAsync(userPrincipalName: true)).Value.AccessControlList.ToList();

		//	var owner = folderOwner.Replace('@', '_').ToLower();
		//	var ownerAcl = acl.FirstOrDefault(p => p.EntityId != null && p.EntityId.Replace('@', '_').ToLower() == owner);
		//	if (ownerAcl != null)
		//	{
		//		if (ownerAcl.Permissions.HasFlag(targetPermissions))
		//		{
		//			result.Success = true;
		//			return result;                    // Exit Early, no changes needed
		//		}
		//		ownerAcl.Permissions = targetPermissions;
		//	}
		//	else
		//	{
		//		acl.Add(new PathAccessControlItem(AccessControlType.User, targetPermissions, false, folderOwner));
		//	}

		//	return SetRootACL(fileSystem, acl);
		//}

		//private Result SetRootACL(string fileSystem, List<PathAccessControlItem> acl)
		//{
		//	var result = new Result();
		//	try
		//	{
		//		var directoryClient = dlsClient.GetFileSystemClient(fileSystem).GetDirectoryClient(string.Empty);

		//		// Update root container's ACL
		//		var response = directoryClient.SetAccessControlList(acl);
		//		result.Success = response.GetRawResponse().Status == ((int)HttpStatusCode.OK);
		//		result.Message = result.Success ? null : "Error on trying to add Folder Owner as Execute on the root Container. Error 500.";
		//		return result;
		//	}
		//	catch (Exception ex)
		//	{
		//		result.Message = ex.Message;
		//		return result;
		//	}
		//}

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
				Debug.WriteLine(ex.Message);
			}
			return fileSystems;
		}

		//public IEnumerable<string> GetContainersForUpn(string upn)
		//{
		//	upn = upn.Replace('@', '_').ToLower();     // Translate for guest accounts
		//	List<FileSystemItem> fileSystems;

		//	try
		//	{
		//		fileSystems = dlsClient.GetFileSystems().ToList();
		//	}
		//	catch (Exception ex)
		//	{
		//		Debug.WriteLine(ex.Message);
		//		throw;
		//	}

		//	var containers = new List<string>();
		//	Parallel.ForEach(fileSystems, filesystem =>
		//	{
		//		var fsClient = dlsClient.GetFileSystemClient(filesystem.Name);
		//		var rootClient = fsClient.GetDirectoryClient(string.Empty);  // container (root)
		//		var acl = rootClient.GetAccessControl(userPrincipalName: true);

		//		if (acl.Value.AccessControlList.Any(
		//			p => p.EntityId?.Replace('@', '_').ToLower().StartsWith(upn) == true))
		//		{
		//			containers.Add(filesystem.Name);
		//		}
		//	});

		//	return containers;
		//}

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

			// Create File System
			var result = new Result();
			try
			{
				var dlFileSystemResponse = await dlsClient.CreateFileSystemAsync(fileSystemName, PublicAccessType.None, metadata);
				//var dlfsClient = dlFileSystemResponse.Value;
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

