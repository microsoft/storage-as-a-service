
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Files.DataLake;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.IO;
using Xunit;

namespace Microsoft.UsEduCsu.Saas.Tests
{
	public class FileSystemTests
	{
		private readonly ILogger log = new LoggerFactory().CreateLogger<FileSystemTests>();

		public FileSystemTests()
		{
			ConfigureEnvironmentVariablesFromLocalSettings();
		}

		[Fact]
		public void GetFileSystemDetails()
		{
			var account = "stsaasdemoeastus0202";
			var x = FileSystems.GetFileSystemDetailsForAccount(account);

			Assert.True(x.Count > 0);

		}

		//  [Fact] TODO: Should I remove this?
		public async void CreateManyFileSystems()
		{
			var owner = "john@contosou.com";
			var userOperations = new UserOperations(log, new DefaultAzureCredential());
			var ownerId = ""; //await userOperations.GetObjectIdFromUPN(owner);

			var account = "stsaasdemoeastus0202";
			var storageUri = new Uri($"https://{account}.dfs.core.windows.net");
			var fileSystemOperations = new FileSystemOperations(log, new DefaultAzureCredential(), storageUri);
			var roleOperations = new RoleOperations(log);

			var rng = new Random();

			// Create Lots of FileSystems
			for (int i = 0; i < 100; i++)
			{
				var rndValue = rng.Next(1000);
				var fileSystem = $"fs{rndValue}";
				var result = await fileSystemOperations.CreateFileSystem(fileSystem, owner, $"{rndValue}");

				roleOperations.AssignRoles(account, fileSystem, ownerId);

				log.LogTrace(result.Message);
			}
		}

		//	[Fact] TODO: Should I remove this?
		public void DeleteAllFileSystems()
		{
			var account = "stsaasdemoeastus0202";
			var storageUri = new Uri($"https://{account}.dfs.core.windows.net");

			var tokenCredential = new DefaultAzureCredential();
			var dlsClient = new DataLakeServiceClient(storageUri, tokenCredential);
			var filesystems = dlsClient.GetFileSystems();

			foreach (var filesystem in filesystems)
			{
				dlsClient.DeleteFileSystem(filesystem.Name);
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
