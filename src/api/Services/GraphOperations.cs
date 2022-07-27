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
	public class GraphOperations
	{
		private readonly ILogger log;
		private readonly GraphServiceClient graphClient;
		private readonly CancellationToken graphClientCancellationToken;
		private readonly CacheHelper _cache;

		public GraphOperations(ILogger log, TokenCredential tokenCredential)
		{
			this.log = log;
			this._cache = CacheHelper.GetRedisCacheHelper(log);
			this.graphClientCancellationToken = new CancellationToken();
			this.graphClient = CreateGraphClient(tokenCredential, graphClientCancellationToken).Result;
		}

		public string GetObjectIdFromGroupName(string groupName)
		{
			try
			{
				// Retrieve groups with displayname matching
				var groupsTask = graphClient.Groups
					.Request()
					.Header("ConsistencyLevel", "eventual")
					.Filter($"displayName eq '{groupName}'")
					.Select("id,displayName")
					.GetAsync(graphClientCancellationToken);
				var groups = groupsTask.Result;

				return groups.FirstOrDefault()?.Id; // TODO: Opportunity for caching
			}
			catch (Exception ex)
			{
				log.LogInformation(ex, "Group {0} not found", groupName);
				return null;
			}
		}

		/// <summary>
		/// Looks up the name of the AAD group with the specified object ID.
		/// </summary>
		/// <param name="groupObjectId">The group's AAD object ID.</param>
		/// <returns>The name of the AAD group with the specified object ID, or an empty string if there is no such group.</returns>
		public string GetGroupNameFromObjectId(string groupObjectId)
		{
			try
			{
				// Retrieve groups with group object ID matching
				var groupTask = graphClient.Groups[groupObjectId]
					.Request()
					.GetAsync(graphClientCancellationToken);
				var group = groupTask.Result;

				if (group != null)
					return group.DisplayName;

				return string.Empty;
			}
			catch (Exception ex)
			{
				log.LogInformation(ex, "Group {0} not found", groupObjectId);
				return string.Empty;
			}
		}

		public string GetObjectIdFromUPN(string upn)
		{
			var dirObj = GetDirectoryObject(upn);
			return dirObj?.Id;
		}

		public string GetDisplayName(string userIdentifier) {
			var dirObj = GetDirectoryObject(userIdentifier);
			if (dirObj == null)
				return null;
			if (dirObj.ODataType == "#microsoft.graph.user")
				return ((User) dirObj).DisplayName;
			if (dirObj.ODataType == "#microsoft.graph.servicePrincipal")
				return ((ServicePrincipal) dirObj).DisplayName;
			if (dirObj.ODataType == "#microsoft.graph.group")
				return ((Group) dirObj).DisplayName;

			return dirObj.AdditionalData["displayName"]?.ToString();
		}

		private DirectoryObject GetDirectoryObject(string principalId)
		{
			Func<DirectoryObject> GetDirectoryObjectMethod = () => {
				try
				{
					// Retrieve a user by userPrincipalName
					var myTask = graphClient
						.DirectoryObjects[principalId]
						.Request()
						.GetAsync(graphClientCancellationToken);

					var directoryObject =  myTask.Result;
					return directoryObject;
				}
				catch (Exception ex)
				{
					log.LogInformation(ex, "User {0} not found", principalId);
				return null;
				}
			};

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
}