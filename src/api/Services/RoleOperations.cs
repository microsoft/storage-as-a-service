using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Management.Authorization;
using Microsoft.Azure.Management.Authorization.Models;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.OData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas.Services
{
	public class RoleOperations
	{
		// https://blogs.aaddevsup.xyz/2020/05/using-azure-management-libraries-for-net-to-manage-azure-ad-users-groups-and-rbac-role-assignments/

		private readonly ILogger log;
		private TokenCredentials tokenCredentials;

		// Caches the list of storage plane data role definitions
		private static IList<RoleDefinition> roleDefinitions;

		public RoleOperations(ILogger log)
		{
			this.log = log;
		}

		private void VerifyToken()
		{
			if (tokenCredentials == null)
			{
				var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
				var accessToken = new DefaultAzureCredential().GetToken(tokenRequestContext);
				tokenCredentials = new TokenCredentials(accessToken.Token);
				// TODO: Determine when to expire the tokenCredentials object based on accessToken.ExpiresOn
			}
		}

		private string GetAccountResourceId(string account)
		{
			string accountResourceId = string.Empty;
			try
			{
				VerifyToken();
				var resourceGraphClient = new ResourceGraphClient(tokenCredentials);

				var query = new QueryRequest();
				query.Query = $"resources | where name == '{account}' and type == 'microsoft.storage/storageaccounts' and kind == 'StorageV2' and properties['isHnsEnabled'] | project id";
				var queryResponse = resourceGraphClient.Resources(query);
				if (queryResponse.Count > 0)
				{
					dynamic data = queryResponse;

					var data2 = (Newtonsoft.Json.Linq.JArray)queryResponse.Data;
					var data3 = data2.First;
					accountResourceId = data3.SelectToken("id").ToString();

				}
			}
			catch (Exception)
			{
				throw;
			}

			return accountResourceId;
		}

		private RoleAssignment AddRoleAssignment(string scope, string roleName, string principalId)
		{
			VerifyToken();

			var amClient = new AuthorizationManagementClient(tokenCredentials);
			var roleDefinitions = amClient.RoleDefinitions.List(scope);
			var roleDefinition = roleDefinitions.First(x => x.RoleName == roleName);

			// TODO: Add OData Filter
			var roleAssignments = amClient.RoleAssignments.ListForScope(scope);
			var roleAssignment = roleAssignments.FirstOrDefault(ra => ra.PrincipalId == principalId && ra.RoleDefinitionId == roleDefinition.Id);

			// Create New Role Assignment
			if (roleAssignment is null)
			{
				var racp = new RoleAssignmentCreateParameters(roleDefinition.Id, principalId);
				var roleAssignmentId = Guid.NewGuid().ToString();
				roleAssignment = amClient.RoleAssignments.Create(scope, roleAssignmentId, racp);
			}

			return roleAssignment;
		}

		public Result AssignRoles(string account, string container, string ownerId)
		{
			var result = new Result();

			try
			{
				// Get Storage Account Resource ID
				var accountResourceId = GetAccountResourceId(account);

				// Create Role Assignments
				string containerScope = $"{accountResourceId}/blobServices/default/containers/{container}";

				// Allow user to manage ACL for container
				_ = AddRoleAssignment(containerScope, "Storage Blob Data Owner", ownerId);

				result.Success = true;
			}
			catch (Exception ex)
			{
				// TODO: Consider customizing error message
				log.LogError(ex, ex.Message);
				result.Message = ex.Message;
			}

			return result;
		}

		/// <summary>
		/// Retrieves any Azure Storage data plane roles (Storage Blob Data *) roles for the specified
		/// principal ID on the specified subscription.
		/// </summary>
		/// <param name="subscriptionId"></param>
		/// <param name="principalId"></param>
		/// <returns>A list of storage accounts and containers where the specified principal has the storage data plane role.</returns>
		public IList<StorageDataPlaneRole> GetStorageDataPlaneRoles(string subscriptionId, string principalId)
		{
			// TODO: Consider creating a TokenCredentialManager
			// (local var) TokenCredentials tc = TokenCredentialManager.GetToken();
			VerifyToken();

			// Get Auth Management Client, initialized with the current subscription ID
			var amClient = new AuthorizationManagementClient(tokenCredentials)
			{
				SubscriptionId = subscriptionId
			};

			// Find all the applicable built-in role definition IDs that would give a
			// principal access to storage account data plane
			// TODO: Abstract into VerifyRoleCredentials() method
			if (roleDefinitions == null)
			{
				roleDefinitions = amClient.RoleDefinitions.List($"/subscriptions/{subscriptionId}/")
					.Where(rd => rd.RoleName.StartsWith("Storage Blob Data")
							&& rd.RoleType.Equals("BuiltInRole", StringComparison.OrdinalIgnoreCase))
					.ToList();
			}

			// Retrieve the applicable role assignments scoped to containers for the specified AAD principal
			var roleDefinitionIds = roleDefinitions.Select(rd => rd.Id);    // Create an IList<string> of the role definition IDs

			// Filter the role assignments query by the AAD object ID of the signed in user
			ODataQuery<RoleAssignmentFilter> q = new ODataQuery<RoleAssignmentFilter>();
			q.Filter = $"assignedTo('{principalId}')";

			// HACK: What's the point of this?
			//RoleAssignmentFilter raf = new RoleAssignmentFilter(principalId);
			//System.Diagnostics.Debug.WriteLine(raf.ToString());

			/* Query the subscription's role assignments for the specified principal.
			 * This will only return role assignments where the provided token has
			 * Microsoft.Authorization/roleAssignments/read authorization.
			 * For example, by granting the app registration User Access Administrator on storage accounts
			 * in the specified subscription, this call will return any role assignment granted on the storage accounts.
			 * NOTE: Storage-as-a-service does not currently support role assignments at levels higher than storage account.
			 * I.e., a storage data plane role assignment at the resource group level or higher will not be reflected correctly.
			 */
			IPage<RoleAssignment> res = amClient.RoleAssignments.ListForSubscription(q);
			return res
				// Filter for storage data plane roles
				// This cannot be done on the server side
				.Where(ra => roleDefinitionIds.Contains(ra.RoleDefinitionId))
				// Transform the result set in a custom object
				.Select(ra => new StorageDataPlaneRole()
				{
					RoleName = roleDefinitions.Single(rd => rd.Id.Equals(ra.RoleDefinitionId)).RoleName,
					Scope = ra.Scope,
					PrincipalType = ra.PrincipalType,
					PrincipalId = ra.PrincipalId
				})
			 	.ToList();
		}

		/// <summary>
		/// Return a list of containers where the specified AAD principal has data plane access.
		/// </summary>
		/// <param name="account">The storage account for which to retrieve container access.</param>
		/// <param name="principalId">The AAD principal for which to retrieve role assignments.</param>
		/// <returns>An List<ContainerRole>.</returns>
		public List<ContainerRole> GetContainerRoleAssignments(string account, string principalId)
		{
			// TODO: Consider creating a TokenCredentialManager
			// (local var) TokenCredentials tc = TokenCredentialManager.GetToken();
			VerifyToken();

			// Get Storage Account Resource ID
			var accountResourceId = GetAccountResourceId(account);

			// Get Auth Management Client
			var amClient = new AuthorizationManagementClient(tokenCredentials);

			// Find all the applicable built-in role definition IDs that would give a principal access to storage account data plane
			if (roleDefinitions == null)
			{
				roleDefinitions = amClient.RoleDefinitions.List(accountResourceId)
					.Where(rd => rd.RoleName.StartsWith("Storage Blob Data")
							&& rd.RoleType.Equals("BuiltInRole", StringComparison.OrdinalIgnoreCase))
					.ToList();
			}

			// Retrieve the applicable role assignments scoped to containers for the specified AAD principal
			var roleDefinitionIds = roleDefinitions.Select(rd => rd.Id);    // Create an IList<string> of the role definition IDs
																			// TODO: Add ODataQuery to filter for principal ID
			var roleAssignments = amClient.RoleAssignments.ListForScope(accountResourceId)
				.Where(ra => ra.Scope.Contains("/blobServices/default/containers/")
					&& roleDefinitionIds.Contains(ra.RoleDefinitionId)
					&& ra.PrincipalId == principalId)
				// Transform matching role assignments into the method's return value
				.Select(ra => new ContainerRole()
				{
					RoleName = roleDefinitions.Single(rd => rd.Id.Equals(ra.RoleDefinitionId)).RoleName,
					Container = ra.Scope.Split('/').Last(),
					PrincipalId = ra.PrincipalId
				})
				.ToList();

			return roleAssignments;
		}

		public class ContainerRole
		{
			public string RoleName { get; set; }
			public string Container { get; set; }
			public string PrincipalId { get; set; }
		}

		public class StorageDataPlaneRole
		{
			public string RoleName { get; set; }
			public string Scope { get; set; }
			public string PrincipalId { get; set; }
			public string PrincipalType { get; set; }
		}
	}
}
