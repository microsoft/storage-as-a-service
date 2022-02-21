
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using Xunit;

using System;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.IO;
using Azure.Core;

namespace Microsoft.UsEduCsu.Saas.Tests
{
    public class FileSystemTests
    {

        ILogger log = new LoggerFactory().CreateLogger<FileSystemTests>();


		[Fact]
        public async void CreateManyFileSystems()
        {
            ConfigureEnvironmentVariablesFromLocalSettings();

            var account = "stsaasdemoeastus0202";
            var owner = "john@researchuniversity.onmicrosoft.com";
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            var tokenCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
           // var authRecord = tokenCredential.
            var storageUri = new Uri($"https://{account}.dfs.core.windows.net");
            var fileSystemOperations = new FileSystemOperations(log, tokenCredential, storageUri);
            var rng = new Random();

            // Create Lots of Accounts
            var rndValue = rng.Next(1000);
            var result = await fileSystemOperations.CreateFileSystem($"fs{rndValue}", owner, $"{rndValue}");
            log.LogTrace(result.Message);
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
