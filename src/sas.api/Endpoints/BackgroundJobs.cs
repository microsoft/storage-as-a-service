using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using sas.api.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace sas.api
{
	public static class BackgroundJobs
	{
		[FunctionName("CalculateAllFolderSizes")]
		public static async Task<IActionResult> CalculateAllFolderSizes(
			[HttpTrigger(AuthorizationLevel.Function, "POST", Route = "Configuration/CalculateFolderSizes")]
			HttpRequest req, ILogger log)
		{
			if (!SasConfiguration.ValidateSharedKey(req, SasConfiguration.ApiKey.Configuration))
			{
				return new UnauthorizedResult();
			}

			var configResult = SasConfiguration.GetConfiguration();
			var sb = new System.Text.StringBuilder();

			foreach (var account in configResult.StorageAccounts)
			{
				var serviceUri = new Uri($"https://{account}.dfs.core.windows.net");
				var serviceCLient = CreateDlsClientForUri(serviceUri);
				var fileSystems = serviceCLient.GetFileSystems();

				var msg = $"Analyzing {account}";
				log.LogInformation(msg);
				sb.AppendLine(msg);

				foreach (var filesystem in fileSystems)
				{
					var containerUri = new Uri($"https://{account}.dfs.core.windows.net/{filesystem.Name}");
					var containerClient = CreateDlsClientForUri(serviceUri);
					var fileSystemClient = containerClient.GetFileSystemClient(filesystem.Name);
					var folders = fileSystemClient.GetPaths().Where<PathItem>(
						pi => pi.IsDirectory == null ? false : (bool)pi.IsDirectory);

					var folderOperations = new FolderOperations(serviceUri, filesystem.Name, log);

					long size = 0;
					foreach (var folder in folders)
					{
						size += await folderOperations.CalculateFolderSize(folder.Name);
					}

					msg = $"  {filesystem.Name} aggregate size {size} bytes";
					log.LogInformation(msg);
					sb.AppendLine(msg);
				}
			}

			return new OkObjectResult(sb.ToString());
		}

		private static DataLakeServiceClient CreateDlsClientForUri(Uri containerUri)
		{
			return new DataLakeServiceClient(containerUri, new DefaultAzureCredential());
		}
	}
}
