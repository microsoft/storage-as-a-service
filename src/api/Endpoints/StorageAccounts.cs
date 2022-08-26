using System;
using System.Collections.Concurrent;
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
			// Get Storage Account Uri
			var storageUri = SasConfiguration.GetStorageUri(account);

			// Get Storage Account Client
			var ApiCredential = new DefaultAzureCredential();
			var storageAccountClient = new DataLakeServiceClient(storageUri, ApiCredential);

			// Setup the Role Operations
			var roleOperations = new RoleOperations(log);

			// Get Container List
			var accessibleContainers = new StorageAccountOperations(log)
				.GetAccessibleContainerDetails(principalId, account);

			// User Operations
			var graphOps = new GraphOperations(log, ApiCredential);

			// Initilize the result
			ConcurrentBag<ContainerDetail> containerDetails = new();

			// Need to get the filesytems
			try
			{
				var filesystems = storageAccountClient.GetFileSystems(FileSystemTraits.Metadata)
						.Where(c => accessibleContainers.Contains(c.Name))  // Filter for the future
						.Select(l => new { Name = l.Name, Properties = l.Properties })
						.ToList();

				string accountResourceId = roleOperations.GetAccountResourceId(account);

				// Build additional details
				Parallel.ForEach(filesystems, (fs) =>
				{
					var cd = GetContainerDetail(roleOperations, graphOps, account, accountResourceId,
						fs.Name, fs.Properties, log);
					containerDetails.Add(cd);
				});
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
			}

			// Return result
			return containerDetails
				.OrderBy(s => s.Name) // using Extension method, but outside of concurrent thread
				.ThenBy(c => c.Name)
				.ToList();
		}

		private static ContainerDetail GetContainerDetail(RoleOperations roleOps, GraphOperations graphOps,
			string account, string accountResourceId, string container, FileSystemProperties properties,
			ILogger log)
		{
			// Calculate Cost Per TB
			decimal costPerTB = 0.0M;
			if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COST_PER_TB")))
				_ = decimal.TryParse(Environment.GetEnvironmentVariable("COST_PER_TB"), out costPerTB);

			// Get Metadata information
			var metadata = (properties.Metadata ?? new Dictionary<string, string>());
			long? size = metadata.ContainsKey("Size") ? long.Parse(metadata["Size"], CultureInfo.CurrentCulture) : null;
			decimal? cost = (size == null) ? null : size * costPerTB / 1000000000000;
			metadata["Size"] = size.HasValue ? ConvertFromBytes(size.Value) : String.Empty;
			metadata["Cost"] = cost.HasValue ? cost.Value.ToString("C", CultureInfo.CurrentCulture) : String.Empty;
			metadata["LastModified"] = properties.LastModified.ToString("G", CultureInfo.CurrentCulture);

			// Rbac Principal Types to Display
			var validTypes = new[] { "Group", "User" };
			var sortOrderMap = new Dictionary<string, int>() {
				{ "Storage Blob Data Owner", 1 },
				{ "Storage Blob Data Contributor", 2 },
				{ "Storage Blob Data Reader", 3 } };

			// Determine Access Roles
			var roles = roleOps.GetStorageDataPlaneRoles(accountResourceId, container);
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

			// Package in ContainerDetail
			var uri = SasConfiguration.GetStorageUri(account, container).ToString();
			var cd = new ContainerDetail()
			{
				Name = container,
				Metadata = metadata,
				Access = rbacEntries,
				StorageExplorerDirectLink = $"storageexplorer://?v=2&tenantId={SasConfiguration.TenantId}&type=fileSystem&container={container}&serviceEndpoint={HttpUtility.UrlEncode(uri)}",
				Uri = uri
			};

			return cd;
		}

		private static string ConvertFromBytes(long size)
		{
			(double result, string postfix) = size switch
			{
				>= 1000000000000 => (size / 1000000000000, "TB"),
				>= 1000000000 => (size / 1000000000, "GB"),
				>= 1000000 => (size / 1000000, "MB"),
				>= 1000 => (size / 1000000, "KB"),
				_ => (size, "Bytes")
			};
			return $"{result.ToString("N0", CultureInfo.CurrentCulture)} {postfix}";
		}
	}
}
