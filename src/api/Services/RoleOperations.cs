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
using Microsoft.UsEduCsu.Saas.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

		internal string GetAccountResourceId(string account)
		{
			// TODO: Move to ResourceOperations class?

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

		/// <summary>
		/// Retrieves a complete list of storage accounts and containers in those storage accounts
		/// that the specified principal ID can access based on RBAC data plane roles.
		/// </summary>
		/// <param name="principalId"></param>
		/// <returns></returns>
		internal IList<StorageAccountAndContainers> GetAccessibleContainersForPrincipal(string principalId)
		{
			ArgumentNullException.ThrowIfNull(principalId, nameof(principalId));

			var appCred = new DefaultAzureCredential();

			// TODO: Foreach parallel (?) for subscriptions
			var SubscriptionId = SasConfiguration.ManagedSubscriptions;

			// TODO: Getting them out in order of storage account to make processing more efficient?
			IList<StorageDataPlaneRole> roleAssignments = GetStorageDataPlaneRoles(SubscriptionId, principalId);

			// TODO: Unit test for pattern
			// TODO: Consider having class-level (static) scoped compiled Regex instance
			const string ScopePattern = @"^/subscriptions/[0-9a-f-]{36}/resourceGroups/[\w_\.-]{1,90}/providers/Microsoft.Storage/storageAccounts/(?<accountName>\w{3,24})(/blobServices/default/containers/(?<containerName>[\w-]{3,63}))?$";
			Regex re = new(ScopePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

			List<StorageAccountAndContainers> results = new();

			// Process the role assignments into storage account and container names
			foreach (var sdpr in roleAssignments)
			{
				// Determine if this is a storage account or container assignment
				// (No support currently for higher-level assignments, it would require a list of storage accounts.)
				Match m = re.Match(sdpr.Scope);

				if (m.Success)
				{
					// There will always be a storage account name if there was a Regex match
					string storageAccountName = m.Groups["accountName"].Value;

					// Find an existing entry for this storage account in the result set
					StorageAccountAndContainers fsr = results
						.SingleOrDefault(fsr => fsr.StorageAccountName.Equals(storageAccountName, StringComparison.OrdinalIgnoreCase));

					// If this is the first time we've encountered this storage account
					if (fsr == null)
					{
						// Set the storage account name property and add to result set
						fsr = new StorageAccountAndContainers() { StorageAccountName = storageAccountName };
						results.Add(fsr);
					}

					// If there are potentially containers in this storage account that aren't listed yet
					if (!fsr.AllContainers)
					{
						// Determine if this is a container-level assignment
						// that hasn't been added to the list of containers yet
						if (m.Groups["containerName"].Success
							&& !fsr.Containers.Contains(m.Groups["containerName"].Value))
						{
							// Assume access is only to this container
							fsr.Containers.Add(m.Groups["containerName"].Value);
						}
						else
						{
							// The role assignment applies to the entire storage account
							var serviceUri = SasConfiguration.GetStorageUri(fsr.StorageAccountName);

							// Access is to entire storage account; retrieve all containers
							var adls = new FileSystemOperations(log, appCred, serviceUri);
							var containers = adls.GetContainers();

							// Replace any previously included containers
							fsr.Containers = containers.Select(fs => fs.Name).ToList();

							// There can't be any more containers in this storage account
							fsr.AllContainers = true;
						}
					}
				}
				else
				{
					// TODO: Log that scope format doesn't match expectation
				}
			}

			return results;
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
		/// Retrieves all Azure Storage data plane roles (Storage Blob Data *) for the specified
		/// storage container.
		/// </summary>
		/// <param name="containerResourceId">The Azure resource ID of the storage account container,
		/// like /subscriptions/[subscription id]/resourceGroups/[resource group name]/providers/Microsoft.Storage/storageAccounts/[storage account]/blobServices/default/containers/[container name]</param>
		/// <returns></returns>
		public IList<StorageDataPlaneRole> GetStorageDataPlaneRoles(string containerResourceId)
		{
			// https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{parentResourcePath}/{resourceType}/{resourceName}/providers/Microsoft.Authorization/roleAssignments?$filter={$filter}&api-version=2015-07-01
			return GetStorageDataPlaneRolesByScope(containerResourceId);
		}

		private IList<StorageDataPlaneRole> GetStorageDataPlaneRolesByScope(string scope, string principalId = null)
		{
			// TODO: Consider creating a TokenCredentialManager
			// (local var) TokenCredentials tc = TokenCredentialManager.GetToken();
			VerifyToken();

			// Get Auth Management Client, initialized with the current subscription ID
			var amClient = new AuthorizationManagementClient(tokenCredentials);

			// Find all the applicable built-in role definition IDs that would give a
			// principal access to storage account data plane
			// TODO: Abstract into VerifyRoleCredentials() method
			if (roleDefinitions == null)
			{
				roleDefinitions = amClient.RoleDefinitions.List(scope)
					.Where(rd => rd.RoleName.StartsWith("Storage Blob Data")
							&& rd.RoleType.Equals("BuiltInRole", StringComparison.OrdinalIgnoreCase))
					.ToList();
			}

			// Retrieve the applicable role assignments scoped to containers for the specified AAD principal
			var roleDefinitionIds = roleDefinitions.Select(rd => rd.Id);    // Create an IList<string> of the role definition IDs

			ODataQuery<RoleAssignmentFilter> q = null;

			// Filter the role assignments query by the AAD object ID of the signed in user
			if (principalId != null)
			{
				q = new ODataQuery<RoleAssignmentFilter>();
				q.Filter = $"assignedTo('{principalId}')";
			}

			/* Query the subscription's role assignments for the specified principal.
			 * This will only return role assignments where the provided token has
			 * Microsoft.Authorization/roleAssignments/read authorization.
			 * For example, by granting the app registration User Access Administrator on storage accounts
			 * in the specified subscription, this call will return any role assignment granted on the storage accounts.
			 * NOTE: Storage-as-a-service does not currently support role assignments at levels higher than storage account.
			 * I.e., a storage data plane role assignment at the resource group level or higher will not be reflected correctly.
			 GET https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{parentResourcePath}/{resourceType}/{resourceName}/providers/Microsoft.Authorization/roleAssignments?$filter={$filter}&api-version=2015-07-01
			 */
			IPage<RoleAssignment> res = null;
			try {
				res = amClient.RoleAssignments.ListForScope(scope, q);
			}
			catch (Exception ex) {
				log.LogError(ex, ex.Message);
			}

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
		/// Retrieves any Azure Storage data plane roles (Storage Blob Data *) for the specified
		/// principal ID on the specified subscription.
		/// </summary>
		/// <param name="subscriptionId"></param>
		/// <param name="principalId"></param>
		/// <returns>A list of storage accounts and containers where the specified principal has the storage data plane role.</returns>
		public IList<StorageDataPlaneRole> GetStorageDataPlaneRoles(string subscriptionId, string principalId)
		{
			string Scope = $"/subscriptions/{subscriptionId}/";

			return GetStorageDataPlaneRolesByScope(Scope, principalId);
		}

		/// <summary>
		/// Return a list of containers where the specified AAD principal has data plane access.
		/// </summary>
		/// <param name="account">The storage account for which to retrieve container access.</param>
		/// <param name="principalId">The AAD principal for which to retrieve role assignments.</param>
		/// <returns>An List<ContainerRole>.</returns>
		public List<ContainerRole> GetContainerRoleAssignments(string account, string principalId)
		{
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
					PrincipalId = ra.PrincipalId,
					Id = ra.Id
				})
				.ToList();

			return roleAssignments;
		}

		internal void DeleteRoleAssignment(string roleAssignmentId)
		{
			VerifyToken();
			var amClient = new AuthorizationManagementClient(tokenCredentials);			// Get Auth Management Client
			amClient.RoleAssignments.DeleteById(roleAssignmentId);
		}

		public class ContainerRole
		{
			public string RoleName { get; set; }
			public string Container { get; set; }
			public string PrincipalId { get; set; }
			public string Id { get; set; }
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
