using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Rest;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas.Services
{
	public class GroupOperations
	{
		private readonly ILogger log;
		private readonly GraphServiceClient graphClient;
		private readonly CancellationToken graphClientCancellationToken;

		public GroupOperations(ILogger log, TokenCredential tokenCredential)
		{
			this.log = log;

			this.graphClientCancellationToken = new CancellationToken();
			this.graphClient = CreateGraphClient(tokenCredential, graphClientCancellationToken).Result;
		}

		public async Task<string> GetObjectIdFromGroupName(string groupName)
		{
			try
			{
				// Retrieve groups with displayname matching
				var groups = await graphClient.Groups
					.Request()
					.Header("ConsistencyLevel", "eventual")
					.Filter($"displayName eq '{groupName}'")
					.Select("id,displayName")
					.GetAsync(graphClientCancellationToken);

				return groups.FirstOrDefault()?.Id;
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				return null;
			}
		}

		/// <summary>
		/// Looks up the name of the AAD group with the specified object ID.
		/// </summary>
		/// <param name="groupObjectId">The group's AAD object ID.</param>
		/// <returns>The name of the AAD group with the specified object ID, or an empty string if there is no such group.</returns>
		public async Task<string> GetGroupNameFromObjectId(string groupObjectId)
		{
			try
			{
				// Retrieve groups with group object ID matching
				var group = await graphClient.Groups[groupObjectId]
					.Request()
					.GetAsync(graphClientCancellationToken);

				if (group != null)
					return group.DisplayName;

				return string.Empty;
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				return string.Empty;
			}
		}

		private async Task<GraphServiceClient> CreateGraphClient(TokenCredential tokenCredential,
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
}