using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.UsEduCsu.Saas.Services
{
	public class GroupOperations
	{
		private ILogger log;
		private TokenCredential tokenCredential;

		public GroupOperations(ILogger log, TokenCredential tokenCredential)
		{
			this.log = log;
			this.tokenCredential = tokenCredential;
		}

		public async Task<string> GetObjectIdfromGroupName(string groupName)
		{
			try
			{
				var cancellationToken = new CancellationToken();
				var graphClient = await CreateGraphClient(cancellationToken);

				// Retrieve groups with displayname matching
				var groups = await graphClient.Groups
					.Request()
					.Header("ConsistencyLevel", "eventual")
					.Filter($"displayName eq '{groupName}'")
					.Select("id,displayName")
					.GetAsync(cancellationToken);

				if (groups.Count > 0)
					return groups.First().Id;

				return null;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				return null;
			}
		}

		public async Task<string> GetGroupNameFromObjectId(string gid)
		{
			try
			{
				var cancellationToken = new CancellationToken();
				var graphClient = await CreateGraphClient(cancellationToken);

				// Retrieve groups with displayname matching
				var group = await graphClient.Groups[gid]
					.Request()
					.GetAsync(cancellationToken);

				if (group != null)
					return group.DisplayName;

				return string.Empty;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				return string.Empty;
			}
		}

		private async Task<GraphServiceClient> CreateGraphClient(CancellationToken cancellationToken)
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

			var graphClient = new GraphServiceClient(authProvider);
			return graphClient;
		}
	}
}