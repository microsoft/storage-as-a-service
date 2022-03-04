using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas.Services
{
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

		internal static ConfigurationResult GetConfiguration()
		{
			var dlsa = Environment.GetEnvironmentVariable("DATALAKE_STORAGE_ACCOUNTS");
			var accounts = dlsa.Replace(',', ';').Split(';');
			Array.ForEach(accounts, x => x = x.Trim());
			accounts = accounts.Where(x => x.Length > 0).ToArray();

			// Config
			var result = new ConfigurationResult()
			{
				TenantId = TenantId,
				ClientId = ClientId,
				StorageAccounts = accounts
			};
			return result;
		}

		internal static Uri GetStorageUri(string account, string fileSystem = null)
		{
			var storageUri = new Uri($"https://{account}.dfs.core.windows.net");
			if (fileSystem != null)
				storageUri = new Uri(storageUri, fileSystem);
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

			return ExpectedApiKey.Equals(ReceivedApiKey[0]);
		}

		internal enum ApiKey
		{
			FileSystems = 0,
			Configuration = 1
		}

		internal class ConfigurationResult
		{
			public string[] StorageAccounts { get; set; }
			public string TenantId { get; set; }
			public string ClientId { get; set; }
		}
	}
}
