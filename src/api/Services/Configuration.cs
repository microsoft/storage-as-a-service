// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("tests")]

namespace Microsoft.UsEduCsu.Saas.Services;

// TODO: Rename class
internal static class Configuration
{
	const string FileSystemApiKeySettingName = "FILESYSTEMS_API_KEY";
	internal static string TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
	internal static string ClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
	internal static string ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
	internal static string CacheConnection = Environment.GetEnvironmentVariable("CacheConnection");
	private readonly static string ManagedSubscriptions = Environment.GetEnvironmentVariable("MANAGED_SUBSCRIPTIONS");
	internal static string StorageAccountFriendlyTagNameKey = System.Environment.GetEnvironmentVariable("STORAGE_FRIENDLY_TAG_NAME");
	internal static string StorageAccountPropertiesCacheKey = "storageAccountProperties";
	/// <summary>
	/// Parses a configuration item that contains multiple values
	/// separated by commas or semicolons and returns them as an
	/// array of strings.
	/// </summary>
	/// <param name="value">The raw configuration item.</param>
	/// <returns>The array of strings with one element for value.</returns>
	private static string[] ParseMultiValuedConfigurationValue(string value)
	{
		// TODO: Unit test this method (set access modifier to internal)

		string[] items = value
			.Replace(',', ';')  // Consistency: use only ; as the separator
			.Split(';');

		// Remove leading and trailing spaces from each item
		Array.ForEach(items, s => s = s.Trim());

		// Remove any empty values
		return items.Where(s => s.Length > 0)
			.ToArray();
	}

	internal static string[] GetSubscriptions()
	{
		return ParseMultiValuedConfigurationValue(ManagedSubscriptions);
	}

	internal static Uri GetStorageUri(string account, string fileSystem = null)
	{
		// TODO: This should be in the StorageAccountOperations class

		ArgumentNullException.ThrowIfNull(account, nameof(account));

		var storageUri = new Uri($"https://{account}.dfs.core.windows.net");

		if (!string.IsNullOrWhiteSpace(fileSystem))
		{
			// Append the container name to the end of the URI
			storageUri = new Uri(storageUri, fileSystem);
		}

		return storageUri;
	}

	/// <summary>
	/// Validates the shared key is present and valid in the POST request header
	/// </summary>
	internal static bool ValidateSharedKey(HttpRequest req, ApiKey key)
	{
		string ApiKeySettingName;
		string ApiKeyHeaderName;

		switch (key)
		{
			case ApiKey.FileSystems:
				ApiKeySettingName = FileSystemApiKeySettingName;
				ApiKeyHeaderName = "Saas-FileSystems-Api-Key";
				break;
			default:
				throw new ArgumentException($"Unsupported API key.", nameof(key));
		}

		// TODO: Create as a service and enum the shared keys to use
		// Retrieve the required header from the request
		var ReceivedApiKey = req.Headers[ApiKeyHeaderName];
		if (StringValues.Empty == ReceivedApiKey)
		{
			return false;
		}

		var ExpectedApiKey = Environment.GetEnvironmentVariable(ApiKeySettingName);

		if (ExpectedApiKey == null) throw new MissingConfigurationException(ApiKeySettingName);

		return ExpectedApiKey.Equals(ReceivedApiKey[0], StringComparison.Ordinal);
	}

	internal static (bool IsValid, Dictionary<string, string> errors) Validate()
	{
		var errors = new Dictionary<string, string>();
		if (string.IsNullOrEmpty(TenantId))
			errors.Add("AZURE_TENANT_ID", "Is missing");
		if (string.IsNullOrEmpty(ClientId))
			errors.Add("AZURE_CLIENT_ID", "Is missing");
		if (string.IsNullOrEmpty(ClientSecret))
			errors.Add("AZURE_CLIENT_SECRET", "Is missing");
		if (string.IsNullOrEmpty(CacheConnection))
			errors.Add("CacheConnection", "Is missing");
		if (string.IsNullOrEmpty(ManagedSubscriptions))
			errors.Add("MANAGED_SUBSCRIPTIONS", "Is missing");
		if (string.IsNullOrEmpty(ManagedSubscriptions))
			errors.Add("STORAGE_FRIENDLY_TAG_NAME", "Is missing");
		return (errors.Count == 0, errors);
	}

	internal enum ApiKey
	{
		FileSystems = 0
	}
}
