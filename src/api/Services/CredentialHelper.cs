// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//using Azure.Core;
//using Azure.Identity;
//using Microsoft.Extensions.Logging;
//using System.Security.Authentication;

//namespace Microsoft.UsEduCsu.Saas.Services;

//internal static class CredentialHelper
//{
//	/// <summary>
//	/// Retrieves an on-behalf-of credential to access the Azure Storage API on behalf of the specified principal.
//	/// </summary>
//	/// <param name="log"></param>
//	/// <param name="ownerId"></param>
//	/// <returns></returns>
//	public static TokenCredential GetUserCredentials(ILogger log, string ownerId)
//	{
//		var accessToken = CacheHelper.GetRedisCacheHelper(log).GetAccessToken(ownerId);
//		if (string.IsNullOrEmpty(accessToken))
//			throw new AuthenticationException("Credentials for the user is not found in cache.");

//		// TODO: check access token for expiration, null
//		var userCred = new OnBehalfOfCredential(SasConfiguration.TenantId,
//							SasConfiguration.ApiClientId, SasConfiguration.ApiClientSecret, accessToken);
//		return userCred;
//	}
//}
