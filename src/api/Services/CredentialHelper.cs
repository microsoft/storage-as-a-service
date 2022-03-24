using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class CredentialHelper
	{
		public static TokenCredential GetUserCredentials(ILogger log, string ownerId)
		{
			var accessToken = CacheHelper.GetRedisCacheHelper(log).GetAccessToken(ownerId);
			var userCred = new OnBehalfOfCredential(SasConfiguration.TenantId,
								SasConfiguration.ApiClientId, SasConfiguration.ApiClientSecret, accessToken);
			return userCred;
		}
	}
}
