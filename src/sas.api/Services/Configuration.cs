using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;

namespace sas.api
{
	public static class SasConfiguration
	{
		const string FileSystemApiKeySettingName = "FILESYSTEMS_API_KEY";
		const string ConfigurationApiKeySettingName = "CONFIGURATION_API_KEY";

		internal static ConfigurationResult GetConfiguration()
		{
			var dlsa = Environment.GetEnvironmentVariable("DATALAKE_STORAGE_ACCOUNTS");
			var accounts = dlsa.Replace(',', ';').Split(';');
			// TODO: This doesn't do anything because the result of Trim isn't assigned
			Array.ForEach(accounts, x => x.Trim());
			accounts = accounts.Where(x => x.Length > 0).ToArray();

			// Config
			var result = new ConfigurationResult()
			{
				TenantId = Environment.GetEnvironmentVariable("TENANT_ID"),
				ClientId = Environment.GetEnvironmentVariable("APP_REGISTRATION_CLIENT_ID"),
				StorageAccounts = accounts
			};
			return result;
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
