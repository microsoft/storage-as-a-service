using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal static class CredentialHelper
	{
		/// <summary>
		/// Retrieves an on-behalf-of credential to access the Azure Storage API on behalf of the specified principal.
		/// </summary>
		/// <param name="log"></param>
		/// <param name="ownerId"></param>
		/// <returns></returns>
		public static TokenCredential GetUserCredentials(ILogger log, string ownerId)
		{
			var accessToken = CacheHelper.GetRedisCacheHelper(log).GetAccessToken(ownerId);
			// TODO: check access token for expiration, null
			var userCred = new OnBehalfOfCredential(SasConfiguration.TenantId,
								SasConfiguration.ApiClientId, SasConfiguration.ApiClientSecret, accessToken);
			return userCred;
		}
	}
}
