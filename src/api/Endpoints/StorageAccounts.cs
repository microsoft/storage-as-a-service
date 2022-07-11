using System;
using System.Collections.Generic;
using System.Web;
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
				var containers = GetFileSystemsForAccount(account);
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
	}
}
