// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.UsEduCsu.Saas.Services;

public class ResourceGraphOperations
{
	private ILogger log;
	private TokenCredentials _tokenCredentials;

	public ResourceGraphOperations(ILogger logger, TokenCredential cred)
	{
		log = logger;

		var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
		var accessToken = cred.GetToken(tokenRequestContext, new CancellationToken());

		_tokenCredentials = new Rest.TokenCredentials(accessToken.Token);
	}

	public ResourceGraphOperations(ILogger logger, TokenCredentials creds)
	{
		log = logger;
		_tokenCredentials = creds;
	}

	/// <summary>
	/// Returns the subscription ID (a GUID) embedded in the specified
	/// Azure resource ID.
	/// </summary>
	/// <param name="azureResourceId">A valid Azure resource ID.</param>
	/// <returns>The subscription ID, or null if the scope is not a valid Azure resource ID.</returns>
	public static string ExtractSubscriptionIdFromScope(string azureResourceId)
	{
		const string subIdString = "subscriptionId";
		const string ScopePattern = @"^/subscriptions/(?<" + subIdString + ">[0-9a-f-]{36})/";

		// Extract the subscription ID from the provided scope using regular expression
		Regex re = new(ScopePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);          // TODO: Consider having class-level (static) scoped compiled Regex instance
		Match m = re.Match(azureResourceId);

		if (m.Success)
		{
			return m.Groups[subIdString].Value;
		}

		return null;
	}

	/// <summary>
	/// Uses the Azure Resource Graph to retrieve the Azure resource ID
	/// of the specified ADLS Gen 2 storage account.
	/// </summary>
	/// <param name="storageAccountName">The name of the storage account.</param>
	/// <returns>The Azure resource ID.</returns>
	public string GetAccountResourceId(string storageAccountName)
	{
		string accountResourceId = string.Empty; // TODO: Why not null?

		try
		{
			string queryText = $@"resources
					| where name == '{storageAccountName}' and type == 'microsoft.storage/storageaccounts' and kind == 'StorageV2' and properties['isHnsEnabled']
					| project id";
			QueryResponse queryResponse;

			using (var resourceGraphClient = new ResourceGraphClient(_tokenCredentials))
			{
				var query = new QueryRequest(queryText);
				queryResponse = resourceGraphClient.Resources(query);
			}

			if (queryResponse.Count > 0)
			{
				dynamic data = queryResponse;

				var data2 = (Newtonsoft.Json.Linq.JArray)queryResponse.Data;
				var data3 = data2.First;
				accountResourceId = data3.SelectToken("id").ToString();
			}
			else
			{
				log.LogWarning("Azure Resource Graph query for storage account {0} did not return results. The application identity might not have access to this storage account.", storageAccountName);
				// TODO: Throw custom exception?
			}
		}
		catch (Exception)
		{
			// TODO: Log
			throw;
		}

		return accountResourceId;
	}

	/// <summary>
	/// Uses the Azure Resource Graph to retrieve the Azure resource ID
	/// of the specified ADLS Gen 2 storage account.
	/// </summary>
	/// <param name="storageAccountName">The name of the storage account.</param>
	/// <returns>The Azure resource ID.</returns>
	public string GetAccountResourceTagValue(string storageAccountName, string tagName)
	{
		string accountResourceId = string.Empty; // TODO: Why not null?

		try
		{
			string queryText = $@"resources
					| where name == '{storageAccountName}' and type == 'microsoft.storage/storageaccounts' and kind == 'StorageV2' and properties['isHnsEnabled']
					| project tags";
			QueryResponse queryResponse;

			using (var resourceGraphClient = new ResourceGraphClient(_tokenCredentials))
			{
				var query = new QueryRequest(queryText);
				queryResponse = resourceGraphClient.Resources(query);
			}

			if (queryResponse.Count > 0)
			{
				dynamic data = queryResponse;

				var data2 = (Newtonsoft.Json.Linq.JArray)queryResponse.Data;
				var data3 = data2.First;
				var tags = data3.SelectToken("tags");
				return (string)tags?[tagName];
			}
			else
			{
				log.LogWarning("Azure Resource Graph query for storage account {0} did not return results. The application identity might not have access to this storage account.", storageAccountName);
				// TODO: Throw custom exception?
			}
		}
		catch (Exception)
		{
			// TODO: Log
			throw;
		}

		return accountResourceId;
	}
}