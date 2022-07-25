using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas
{
	public static class BackgroundJobs
	{
		// TODO: Why is this a POST?
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
				var appCred = new DefaultAzureCredential();

				log.LogInformation("Analyzing {account}", account);
				sb.Append("Analyzing ").AppendLine(account);

				// TODO: Consider parallelizing?
				foreach (var filesystem in fileSystems)
				{
					var fileSystemClient = serviceClient.GetFileSystemClient(filesystem.Name);
					var folders = fileSystemClient.GetPaths()
						.Where(pi => pi.IsDirectory == true);

					var folderOperations = new FolderOperations(serviceUri, filesystem.Name, log, appCred);

					long size = 0;
					foreach (var folder in folders)
					{
						size += await folderOperations.CalculateFolderSize(folder.Name);
					}

					// Store Container Level Info
					var metadata = fileSystemClient.GetProperties().Value.Metadata;
					metadata["Size"] = size.ToString(CultureInfo.CurrentCulture);
					metadata.Remove("hdi_isfolder");                    // Strip off a readonly item
					fileSystemClient.SetMetadata(metadata);

					// Report back results
					log.LogInformation("{fileSystemName} aggregate size {size} bytes", filesystem.Name, size);
					sb.Append("  ").Append(filesystem.Name).Append(" aggregate size ").Append(size).AppendLine(" bytes");
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
