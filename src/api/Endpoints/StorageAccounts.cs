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

namespace Microsoft.UsEduCsu.Saas
{
	public static class StorageAccounts
	{
		[ProducesResponseType(typeof(FolderOperations.FolderDetail), StatusCodes.Status200OK)]
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
				var containers = PopulateContainerDetail(account, principalId, log);
				return new OkObjectResult(containers);
			}
		}

		internal static List<ContainerDetail> PopulateContainerDetail(string account, string principalId, ILogger log)
		{
			// Get Environmental Info
			decimal costPerTB = 0.0M;
			if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COST_PER_TB")))
				_ = decimal.TryParse(Environment.GetEnvironmentVariable("COST_PER_TB"), out costPerTB);

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
				var filesystems = storageAccountClient.GetFileSystems()
						.Where(c => accessibleContainers.Contains(c.Name))  // Filter for the future
						.Select(l => new { l.Name, l.Properties, l.Properties.LastModified, l.Properties.Metadata })
						.ToList();

				// User Operations
				var graphOps = new GraphOperations(log, ApiCredential);

				// Rbac Principal Types to Display
				var validTypes = new[] { "Group", "User" };
				var sortOrderMap = new Dictionary<string, int>() {
					{ "Storage Blob Data Owner", 1 },
					{ "Storage Blob Data Contributor", 2 },
					{ "Storage Blob Data Reader", 3 } };

				// Build additional details
				foreach (var fs in filesystems)
				{
					var uri = new Uri(storageUri, fs.Name).ToString();
					var metadata = (Dictionary<string, string>)((fs.Metadata != null) ? fs.Metadata : new Dictionary<string, string>());
					var seEndpoint = HttpUtility.UrlEncode(uri);
					long? size = metadata.ContainsKey("Size") ? long.Parse(metadata["Size"]) : null;
					decimal? cost = (size == null) ? null : size * costPerTB / 1000000000000;

					metadata["Size"] = size.HasValue ? size.Value.ToString("N") : String.Empty;
					metadata["Cost"] = cost.HasValue ? cost.Value.ToString("C") : String.Empty;

					var roles = roleOperations.GetStorageDataPlaneRoles(account: account, container: fs.Name);
					var rbacEntries = roles
						.Where(r => validTypes.Contains(r.PrincipalType))       // Only display User and Groups (no Service Principals)
						.Select(r => new StorageRbacEntry()
						{
							RoleName = r.RoleName.Replace("Storage Blob Data ", string.Empty),
							PrincipalId = r.PrincipalId,
							PrincipalName = graphOps.GetDisplayName(r.PrincipalId),
							Order = sortOrderMap.GetValueOrDefault(r.RoleName)
						})
						.OrderBy(r => r.Order).ThenBy( r => r.PrincipalName
						).ToList();

					var cd = new ContainerDetail()
					{
						Name = fs.Name,
						Metadata = metadata,
						Access = rbacEntries,
						StorageExplorerDirectLink = $"storageexplorer://?v=2&tenantId={SasConfiguration.TenantId}&type=fileSystem&container={fs.Name}&serviceEndpoint={seEndpoint}",
						Uri = uri
					};

					containerDetails.Add(cd);
				}
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
			}

			// Return result
			return containerDetails;
		}
	}
}
