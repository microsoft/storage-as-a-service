
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using Xunit;

using System;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.IO;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Files.DataLake;
using System.Linq;
using Microsoft.Extensions.Azure;
using System.Threading;
using System.Diagnostics;

namespace tests
{
	public class OnBehalfOfTests
	{
		static ILogger logger = new LoggerFactory().CreateLogger<OnBehalfOfTests>();
		static ILogger log = logger;
		string envTenantId, envClientId, envClientSecret;

		public OnBehalfOfTests()
		{
			ConfigureEnvironmentVariablesFromLocalSettings();
			envTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
			envClientId = "44fe5f6b-4e9b-4768-8c0f-cf4d41617ac3";  // Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
			envClientSecret = "j4R7Q~AghSGl~GqMgdHGaPeBPQbAetBmYUsE-"; // Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
		}

		[Fact]
		public async void ReadContainerPathsAsUser()
		{
			// Service Principal Creds
			var appCreds = new DefaultAzureCredential();

			// Get PRincipal Id as App Service Principal
			var owner = "StorageUser@contosou.com";
			var userOperations = new UserOperations(log, appCreds );
			var ownerId = await userOperations.GetObjectIdFromUPN(owner);

			// Get FileSystem as App Service Principal
			var account = "stsaasdemoeastus02";
			var storageUri = new Uri($"https://{account}.dfs.core.windows.net");
			var dlsClient = new DataLakeServiceClient(storageUri, appCreds);
			var fileSystems = dlsClient.GetFileSystems();

			// Get the User Credentials
			var accessToken = CacheHelper.GetRedisCacheHelper(logger).GetAccessToken(ownerId);
			var userCreds = new OnBehalfOfCredential(envTenantId, envClientId, envClientSecret, accessToken);

			// Get Data Lake FileSystem Client OBO User (as User Credentials)
			foreach (var filesystem in fileSystems)
			{
				try
				{
					var fileSystemUri = new Uri(storageUri, filesystem.Name);
					var fileSystemClient = new DataLakeFileSystemClient(fileSystemUri, userCreds);

					// Get Folders
					var paths = fileSystemClient.GetPaths(string.Empty).ToList();
					foreach (var path in paths)
					{
						try
						{
							if (path.IsDirectory.Value)
							{
								var filePaths = fileSystemClient.GetPaths(path.Name).ToList();
								Debug.WriteLine($"{path.Name} file count {filePaths.Count}");
							}
							var fc = fileSystemClient.GetDirectoryClient(path.Name);
							var files = fc.GetPaths();
							Debug.WriteLine($"file count {files.Count()}");
						}
						catch (Exception ex)
						{
							Debug.WriteLine(ex.Message);
						}

					}
				}
				catch(Exception ex)
				{
					Debug.WriteLine($"No access to {filesystem.Name}");
				}
			}
		}

		static void ConfigureEnvironmentVariablesFromLocalSettings()
		{
			var path = Environment.CurrentDirectory;
			var json = File.ReadAllText(Path.Join(path, "local.settings.json"));
			var parsed = Newtonsoft.Json.Linq.JObject.Parse(json).Value<Newtonsoft.Json.Linq.JObject>("Values");

			foreach (var item in parsed)
			{
				Environment.SetEnvironmentVariable(item.Key, item.Value.ToString());
			}
		}
	}
}
