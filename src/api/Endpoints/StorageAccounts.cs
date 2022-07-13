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
		public static async Task<IActionResult> Get(
			[HttpTrigger(AuthorizationLevel.Function, "GET", Route = "StorageAccounts/{account?}")] HttpRequest req,
			ILogger log,  string account)
		{
			ClaimsPrincipalResult cpr = new ClaimsPrincipalResult(UserOperations.GetClaimsPrincipal(req));

			if (!cpr.IsValid) return new UnauthorizedResult();

			var principalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);

			// List of STorage Accounts or List of Containers for a storage account
			if (string.IsNullOrEmpty(account)) {
				StorageAccountOperations sao = new(log);
				var result = sao.GetAccessibleStorageAccounts(principalId);				// TODO: Check for refresh parameter
				return new OkObjectResult(result);
			} else {
				var containers = await PopulateContainerDetail(account, principalId, log);
				return new OkObjectResult(containers);
			}
		}

		private static List<ContainerDetail> GetFileSystemsForAccount(string account)
		{
			var listContainers = new List<ContainerDetail>();
			var name = "ContainerA";
			var storageUri = SasConfiguration.GetStorageUri(account);
			var seEndpoint = HttpUtility.UrlEncode(new Uri(storageUri, name).ToString());
			var x = new ContainerDetail() {
				Name = name,
				StorageExplorerDirectLink = new Uri($"storageexplorer://?v=2&tenantId={SasConfiguration.TenantId}&type=fileSystem&container={name}&serviceEndpoint={seEndpoint}"),
				Metadata = new Dictionary<string,string>() {
					{ "Cost","$1234.56"},
					{ "Size", "100 GB"}
				},
				Access = new List<StorageRbacEntry>() {
					new StorageRbacEntry {RoleName = "Reader", PrincipalName = "John", PrincipalId = "abcd-1234"},
					new StorageRbacEntry {RoleName = "Contributor", PrincipalName = "Sven", PrincipalId = "abcd-1234"}
				}
			};
			listContainers.Add(x);

			return listContainers;
		}


		internal static async Task<List<ContainerDetail>> PopulateContainerDetail(string account, string principalId, ILogger log)
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
			var accountAndContainers = roleOperations.GetAccessibleContainersForPrincipal(principalId);
			var accessibleContainers = accountAndContainers
					.First( a => a.StorageAccountName == account).Containers;

			// Need to get the filesytems
			var filesystems = storageAccountClient.GetFileSystems()
					.Where(c => accessibleContainers.Contains(c.Name))  // Filter for the future
					.Select(l => new { l.Name, l.Properties, l.Properties.LastModified, l.Properties.Metadata })
					.ToList();

			// User Operations
			var uo = new UserOperations(log, ApiCredential);

			// Build additional details
			var containerDetails = new List<ContainerDetail>();
			foreach (var fs in filesystems)
			{
				var metadata = (Dictionary<string,string>) ((fs.Metadata != null) ? fs.Metadata : new Dictionary<string,string>());
				var seEndpoint = HttpUtility.UrlEncode(new Uri(storageUri, fs.Name).ToString());
				long? size = metadata.ContainsKey("Size") ? long.Parse(metadata["Size"]) : null;
				decimal? cost = (size == null) ? null : size * costPerTB / 1000000000000;

				metadata["Size"] = size.HasValue ? size.Value.ToString("N") : String.Empty;
				metadata["Cost"] =cost.HasValue ? cost.Value.ToString("C") : String.Empty;

				//https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{parentResourcePath}/{resourceType}/{resourceName}/providers/Microsoft.Authorization/roleAssignments?$filter={$filter}&api-version=2015-07-01*/
				var xx = roleOperations.GetAccountResourceId(account);
				var resourceType = "Microsoft.Storage/storageAccounts/blobServices/containers";
				var resourceName = fs.Name;
				var containerResourceId = $"{xx}/{resourceType}/{resourceName}/providers/Microsoft.Authorization/roleAssignments";
				var roles = roleOperations.GetStorageDataPlaneRoles(containerResourceId);
				var rbacEntries = roles.Select( r => new StorageRbacEntry() {
										RoleName = r.RoleName.Replace("Storage Blob Data ",string.Empty),
										PrincipalId = r.PrincipalId
									} )
							.ToList();
				rbacEntries.ForEach( async role => role.PrincipalName = await uo.GetDisplayName(role.PrincipalId));

				var cd = new ContainerDetail() {
					Name = fs.Name,
					Metadata = metadata,
					Access = rbacEntries,
					StorageExplorerDirectLink = new Uri($"storageexplorer://?v=2&tenantId={SasConfiguration.TenantId}&type=fileSystem&container={fs.Name}&serviceEndpoint={seEndpoint}")
				};

				containerDetails.Add(cd);
			}

			// Return result
			return containerDetails;
		}
	}
}
