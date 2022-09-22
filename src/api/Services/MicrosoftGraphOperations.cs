// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas.Services;

internal sealed class MicrosoftGraphOperations
{
	private readonly ILogger log;
	private readonly GraphServiceClient graphClient;
	private readonly CancellationToken graphClientCancellationToken;
	private readonly CacheHelper _cache;

	public MicrosoftGraphOperations(ILogger log, TokenCredential tokenCredential)
	{
		this.log = log;
		_cache = CacheHelper.GetRedisCacheHelper(log);
		graphClientCancellationToken = new CancellationToken();
		graphClient = CreateGraphClient(tokenCredential, graphClientCancellationToken).Result;
	}

	internal string GetDisplayName(string userIdentifier)
	{
		var dirObj = GetDirectoryObject(userIdentifier);
		if (dirObj == null)
			return null;
		if (dirObj.ODataType == "#microsoft.graph.user")
			return ((User)dirObj).DisplayName;
		if (dirObj.ODataType == "#microsoft.graph.servicePrincipal")
			return ((ServicePrincipal)dirObj).DisplayName;
		if (dirObj.ODataType == "#microsoft.graph.group")
			return ((Group)dirObj).DisplayName;

		return dirObj.AdditionalData["displayName"]?.ToString();
	}

	private DirectoryObject GetDirectoryObject(string principalId)
	{
		DirectoryObject GetDirectoryObjectMethod()
		{
			try
			{
				// Retrieve a user by userPrincipalName
				var myTask = graphClient
					.DirectoryObjects[principalId]
					.Request()
					.GetAsync(graphClientCancellationToken);

				var directoryObject = myTask.Result;
				return directoryObject;
			}
			catch (Exception ex)
			{
				log.LogInformation(ex, "User {0} not found", principalId);
				return null;
			}
		}

		var dirObj = _cache.DirectoryObjects(principalId, GetDirectoryObjectMethod);
		return dirObj;
	}

	private static async Task<GraphServiceClient> CreateGraphClient(TokenCredential tokenCredential,
		CancellationToken cancellationToken)
	{
		var tokenRequestContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
		var accessToken = await tokenCredential.GetTokenAsync(tokenRequestContext, cancellationToken);

		var authProvider = new DelegateAuthenticationProvider((requestMessage) =>
		{
			requestMessage
				.Headers
				.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
			requestMessage
				.Headers
				.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			return Task.FromResult(0);
		});

		return new GraphServiceClient(authProvider);
	}
}