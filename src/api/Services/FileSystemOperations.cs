// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.UsEduCsu.Saas.Services;

internal sealed class FileSystemOperations
{
	private readonly ILogger _logger;
	private readonly DataLakeServiceClient dlsClient;

	public FileSystemOperations(ILogger log, TokenCredential tokenCredential, string storageAccountName,
		DataLakeClientOptions opts = null)
	{
		_logger = log;
		dlsClient = new DataLakeServiceClient(Configuration.GetStorageUri(storageAccountName), tokenCredential, opts);
	}

	internal FileSystemDetail GetFileSystemDetail(string containerName)
	{
		if (containerName is null) return null;

		var dlfsClient = dlsClient.GetFileSystemClient(containerName);

		if (!FileSystemExists(containerName, dlfsClient))
		{
			return null;
		}

		var metadata = dlfsClient.GetProperties().Value.Metadata ?? new Dictionary<string, string>();

		return new FileSystemDetail()
		{
			Name = containerName,
			StorageExplorerUri = CreateStorageExplorerUri(dlfsClient),
			Uri = dlfsClient.Uri.ToString(),
			Owner = metadata.ContainsKey("Owner") ? metadata["Owner"] : null
		};
	}

	private static string CreateStorageExplorerUri(DataLakeFileSystemClient dlfs)
	{
		var seEndpoint = HttpUtility.UrlEncode(dlfs.Uri.ToString());

		return $"storageexplorer://?v=2&tenantId={Configuration.TenantId}&type=fileSystem&container={dlfs.Name}&serviceEndpoint={seEndpoint}";
	}

	/// <summary>
	/// Determines if the file system associated with this instance exists.
	/// </summary>
	/// <returns>True if the file system exists; otherwise, false.</returns>
	private static bool FileSystemExists(string containerName, DataLakeFileSystemClient dlfs)
	{
		if (string.IsNullOrEmpty(containerName)) throw new ArgumentNullException(nameof(containerName));

		const string ExMsg = "NO SUCH HOST IS KNOWN";

		try
		{
			return dlfs.Exists();
		}
		catch (AggregateException ex) when (ex.InnerExceptions[0].Message.ToUpperInvariant().Contains(ExMsg))
		{
			// The storage account doesn't exist, so the file system doesn't exist
			return false;
		}
		catch (RequestFailedException ex) when (ex.Message.ToUpperInvariant().Contains(ExMsg))
		{
			// The storage account doesn't exist, so the file system doesn't exist
			return false;
		}

		// For other exceptions, return the exception to the caller
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
			_logger.LogError(ex, ex.Message);
		}

		return fileSystems;
	}
}
