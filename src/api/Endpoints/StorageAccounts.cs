using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using static Microsoft.UsEduCsu.Saas.FileSystems;
using Azure.Storage.Files.DataLake.Models;
using System.Text.Json;
using System.Globalization;

namespace Microsoft.UsEduCsu.Saas
{
	public static class StorageAccounts
	{
		[ProducesResponseType(typeof(ContainerDetail), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[FunctionName("StorageAccountsGET")]
		public static IActionResult Get(
			[HttpTrigger(AuthorizationLevel.Function, "GET", Route = "StorageAccounts/{account?}")] HttpRequest req,
			ILogger log, string account)
		{
			// Validate Authorized Principal
			ClaimsPrincipalResult cpr = new ClaimsPrincipalResult(UserOperations.GetClaimsPrincipal(req));
			if (!cpr.IsValid) return new UnauthorizedResult();
			var principalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);

			// List of Storage Accounts or List of Containers for a storage account
			if (string.IsNullOrEmpty(account))
			{
				StorageAccountOperations sao = new(log);
				var result = sao.GetAccessibleStorageAccounts(principalId);             // TODO: Check for refresh parameter
				return new OkObjectResult(result);
			}
			else
			{
				var containerDetails = PopulateContainerDetail(account, principalId, log);
				//TODO: camelCasing - string json = JsonSerializer.Serialize<List<ContainerDetail>>(containerDetails, new JsonSerializerOptions(JsonSerializerDefaults.Web));
				return new OkObjectResult(containerDetails);
			}
		}

		internal static List<ContainerDetail> PopulateContainerDetail(string account, string principalId, ILogger log)
		{
			// Get Account Information
			var storageUri = SasConfiguration.GetStorageUri(account);
			TokenCredential ApiCredential = new DefaultAzureCredential();
			var storageAccountClient = new DataLakeServiceClient(storageUri, ApiCredential);

			// Setup the Role Operations
			var roleOperations = new RoleOperations(log);
			var accessibleContainers = roleOperations.GetAccessibleContainersForPrincipal(principalId)
											.First(a => a.StorageAccountName == account).Containers;

			// Initilize the result
			var containerDetails = new List<ContainerDetail>();

			// Need to get the filesytems
			try
			{
				var filesystems = storageAccountClient.GetFileSystems(FileSystemTraits.Metadata)
						.Where(c => accessibleContainers.Contains(c.Name))  // Filter for the future
						.Select(l => new { Name= l.Name, Properties = l.Properties})
						.ToList();

				// Build additional details
				Parallel.ForEach(filesystems, (fs) =>
				{
					var cd = GetContainerDetail(storageUri, account, fs.Name, fs.Properties, log);
					containerDetails.Add(cd);
				});
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
			}

			// Return result
			return containerDetails;
		}

		private static ContainerDetail GetContainerDetail(Uri storageUri, string account, string container, FileSystemProperties properties, ILogger log)
		{
			var roleOperations = new RoleOperations(log);

			// User Operations
			TokenCredential ApiCredential = new DefaultAzureCredential();
			var graphOps = new GraphOperations(log, ApiCredential);

			// Get Environmental Info
			decimal costPerTB = 0.0M;
			if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COST_PER_TB")))
				_ = decimal.TryParse(Environment.GetEnvironmentVariable("COST_PER_TB"), out costPerTB);

			// Rbac Principal Types to Display
			var validTypes = new[] { "Group", "User" };
			var sortOrderMap = new Dictionary<string, int>() {
				{ "Storage Blob Data Owner", 1 },
				{ "Storage Blob Data Contributor", 2 },
				{ "Storage Blob Data Reader", 3 } };

			var uri = new Uri(storageUri, container).ToString();
			var Metadata = properties.Metadata;
			var metadata = (Dictionary<string, string>)((Metadata != null) ? Metadata : new Dictionary<string, string>());
			var seEndpoint = HttpUtility.UrlEncode(uri);
			long? size = metadata.ContainsKey("Size") ? long.Parse(metadata["Size"]) : null;
			decimal? cost = (size == null) ? null : size * costPerTB / 1000000000000;

			metadata["Size"] = size.HasValue ? ConvertFromBytes(size.Value) : String.Empty;
			metadata["Cost"] = cost.HasValue ? cost.Value.ToString("C") : String.Empty;
			metadata["LastModified"] = properties.LastModified.ToString("G");

			var roles = roleOperations.GetStorageDataPlaneRoles(account: account, container: container);
			var rbacEntries = roles
				.Where(r => validTypes.Contains(r.PrincipalType))       // Only display User and Groups (no Service Principals)
				.Select(r => new StorageRbacEntry()
				{
					RoleName = r.RoleName.Replace("Storage Blob Data ", string.Empty),
					PrincipalId = r.PrincipalId,
					PrincipalName = graphOps.GetDisplayName(r.PrincipalId),
					Order = sortOrderMap.GetValueOrDefault(r.RoleName)
				})
				.OrderBy(r => r.Order).ThenBy(r => r.PrincipalName
				).ToList();

			var cd = new ContainerDetail()
			{
				Name = container,
				Metadata = metadata,
				Access = rbacEntries,
				StorageExplorerDirectLink = $"storageexplorer://?v=2&tenantId={SasConfiguration.TenantId}&type=fileSystem&container={container}&serviceEndpoint={seEndpoint}",
				Uri = uri
			};

			return cd;
		}

		private static string ConvertFromBytes(long size)
		{
			string postfix = "Bytes";
			double result = size;
			if (size >= 1000000000000)
				(result, postfix) = (size / 1000000000000, "TB");
			else if (size >= 1000000000)
				(result, postfix) = (size / 1000000000, "GB");
			else if (size >= 1000000)
				(result, postfix) = (size / 1000000, "MB");
			else if (size >= 1000)
				(result, postfix) = (size / 1000000, "KB");
			return result.ToString("N0") + " " + postfix;
		}
	}
}
