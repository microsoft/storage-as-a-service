// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

[assembly: InternalsVisibleTo("tests")]

namespace Microsoft.UsEduCsu.Saas.Services
{
	// TODO: Rename class
	public static class SasConfiguration
	{
		const string FileSystemApiKeySettingName = "FILESYSTEMS_API_KEY";
		const string ConfigurationApiKeySettingName = "CONFIGURATION_API_KEY";

		internal static string TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
		internal static string ClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
		internal static string ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

		internal static string ApiClientId = Environment.GetEnvironmentVariable("API_CLIENT_ID");
		internal static string ApiClientSecret = Environment.GetEnvironmentVariable("API_CLIENT_SECRET");

		internal static string CacheConnection = Environment.GetEnvironmentVariable("CacheConnection");
		private static string ManagedSubscriptions = Environment.GetEnvironmentVariable("MANAGED_SUBSCRIPTIONS");

		internal static ConfigurationResult GetConfiguration()
		{
			var accounts = ParseMultiValuedConfigurationValue(Environment.GetEnvironmentVariable("DATALAKE_STORAGE_ACCOUNTS"));

			// Config
			var result = new ConfigurationResult()
			{
				TenantId = TenantId,
				ClientId = ClientId,
				StorageAccounts = accounts
			};

			return result;
		}

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
				case ApiKey.Configuration:
					ApiKeySettingName = ConfigurationApiKeySettingName;
					ApiKeyHeaderName = "Saas-Configuration-Api-Key";
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
			if (string.IsNullOrEmpty(ApiClientId))
				errors.Add("API_CLIENT_ID", "Is missing");
			if (string.IsNullOrEmpty(ApiClientSecret))
				errors.Add("API_CLIENT_SECRET", "Is missing");
			if (string.IsNullOrEmpty(CacheConnection))
				errors.Add("CacheConnection", "Is missing");
			if (string.IsNullOrEmpty(ManagedSubscriptions))
				errors.Add("MANAGED_SUBSCRIPTIONS", "Is missing");
			return (errors.Count == 0, errors);
		}


		internal enum ApiKey
		{
			FileSystems = 0,
			Configuration = 1
		}

		internal class ConfigurationResult
		{
			// TODO: Remove StorageAccounts property
			public string[] StorageAccounts { get; set; }
			public string TenantId { get; set; }
			public string ClientId { get; set; }
		}
	}
}
