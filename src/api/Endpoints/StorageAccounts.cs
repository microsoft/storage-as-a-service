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
			[HttpTrigger(AuthorizationLevel.Function, "GET", Route = "StorageAccounts")] HttpRequest req,
			ILogger log)
		{
			ClaimsPrincipalResult cpr = new ClaimsPrincipalResult(UserOperations.GetClaimsPrincipal(req));

			if (!cpr.IsValid) return new UnauthorizedResult();

			var principalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);

			StorageAccountOperations sao = new(log);
			// TODO: Check for refresh parameter
			var result = sao.GetAccessibleStorageAccounts(principalId);

			return new OkObjectResult(result);
		}
	}
}
