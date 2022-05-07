using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas
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
				var serviceUri = SasConfiguration.GetStorageUri(account);
				var serviceClient = CreateDlsClientForUri(serviceUri);
				var fileSystems = serviceClient.GetFileSystems();

				log.LogInformation("Analyzing {account}", account);
				sb.AppendLine($"Analyzing {account}");

				// TODO: Consider parallelizing?
				foreach (var filesystem in fileSystems)
				{
					var containerUri = SasConfiguration.GetStorageUri(account, filesystem.Name);
					var containerClient = CreateDlsClientForUri(serviceUri);
					var fileSystemClient = containerClient.GetFileSystemClient(filesystem.Name);
					var folders = fileSystemClient.GetPaths()
						.Where(pi => pi.IsDirectory == true);

					var folderOperations = new FolderOperations(serviceUri, filesystem.Name, log,
						new DefaultAzureCredential());

					long size = 0;
					foreach (var folder in folders)
					{
						size += await folderOperations.CalculateFolderSize(folder.Name);
					}

					// Store Container Level Info
					var metadata = fileSystemClient.GetProperties().Value.Metadata;
					metadata["Size"] = size.ToString();
					metadata.Remove("hdi_isfolder");                    // Strip off a readonly item
					fileSystemClient.SetMetadata(metadata);

					// Report back results
					log.LogInformation("{fileSystemName} aggregate size {size} bytes", filesystem.Name, size);
					sb.AppendLine($"  {filesystem.Name} aggregate size {size} bytes");
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
