using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Management.Authorization;
using Microsoft.Azure.Management.Authorization.Models;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using Microsoft;
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
		private Rest.TokenCredentials _tokenCredentials;
		private AccessToken _accessToken;
		private CacheHelper _cache;

		// Caches the list of storage plane data role definitions
		private static IList<RoleDefinition> roleDefinitions;

		public RoleOperations(ILogger log)
		{
			this.log = log;
			_cache = CacheHelper.GetRedisCacheHelper(log);
		}

		#region Public and Internal Methods

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

		internal string GetAccountResourceId(string account)
		{
			// TODO: Move to ResourceOperations class?

			string accountResourceId = string.Empty;
			try
			{
				VerifyToken();
				var resourceGraphClient = new ResourceGraphClient(_tokenCredentials);

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
			var SubscriptionId = SasConfiguration.ManagedSubscriptions;         // TODO: Foreach parallel (?) for subscriptions

			IList<StorageDataPlaneRole> roleAssignments = GetStorageDataPlaneRoles(SubscriptionId, principalId);            // TODO: Getting them out in order of storage account to make processing more efficient?

			// TODO: Unit test for pattern
			const string ScopePattern = @"^/subscriptions/[0-9a-f-]{36}/resourceGroups/[\w_\.-]{1,90}/providers/Microsoft.Storage/storageAccounts/(?<accountName>\w{3,24})(/blobServices/default/containers/(?<containerName>[\w-]{3,63}))?$";
			Regex re = new(ScopePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);          // TODO: Consider having class-level (static) scoped compiled Regex instance

			List<StorageAccountAndContainers> results = new();

			// Process the role assignments into storage account and container names
			foreach (var sdpr in roleAssignments)
			{
				// Determine if this is a storage account or container assignment
				// (No support currently for higher-level assignments, it would require a list of storage accounts.)
				Match m = re.Match(sdpr.Scope);
				if (!m.Success)
					continue;   // No Match, move to next one

				// There will always be a storage account name if there was a Regex match
				string storageAccountName = m.Groups["accountName"].Value;

				// Find an existing entry for this storage account in the result set
				StorageAccountAndContainers fsr = results
					.SingleOrDefault(x => x.StorageAccountName.Equals(storageAccountName, StringComparison.OrdinalIgnoreCase));

				// If this is the first time we've encountered this storage account, Set the storage account name property and add to result set
				if (fsr == null)
				{
					fsr = new StorageAccountAndContainers() { StorageAccountName = storageAccountName };
					results.Add(fsr);
				}

				// If there are potentially containers in this storage account that aren't listed yet
				if (!fsr.AllContainers)
				{
					var containerGroup = m.Groups["containerName"];
					// If the container Regex group was successfully parsed
					// but the container hasn't been added to the list yet
					if (containerGroup.Success &&
						!fsr.Containers.Contains(containerGroup.Value))
					{
						fsr.Containers.Add(containerGroup.Value);       // Assume access is only to this container
					}
					// If this is not a container-level assignment
					else if (!containerGroup.Success)
					{
						// The role assignment applies to the entire storage account (at least)
						var serviceUri = SasConfiguration.GetStorageUri(fsr.StorageAccountName);
						var adls = new FileSystemOperations(log, appCred, serviceUri);
						var containers = adls.GetContainers();                      // Access is to entire storage account; retrieve all containers
						fsr.Containers = containers.Select(fs => fs.Name).ToList();     // Replace any previously included containers
						fsr.AllContainers = true;                       // There can't be any more containers in this storage account
					}
				}
			}

			return results;
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
		/// Retrieves all Azure Storage data plane roles (Storage Blob Data *) for the specified
		/// storage container.
		/// </summary>
		/// <param name="account">Storage Account Name</param>
		/// <param name="container">Container Name</param>
		/// <param name="principalId">Optional Principal ID</param>
		/// <returns></returns>
		public IList<StorageDataPlaneRole> GetStorageDataPlaneRoles(string account, string container = null, string principalId = null)
		{
			// /subscriptions/[subscription id]/resourceGroups/[resource group name]/providers/Microsoft.Storage/storageAccounts/[storage account]/blobServices/default/containers/[container name]
			var accountResourceId = GetAccountResourceId(account);
			var scope = accountResourceId;

			if (container != null)
				scope = $"{accountResourceId}/blobServices/default/containers/{container}";

			return GetStorageDataPlaneRolesByScope(scope);
		}

		/// <summary>
		/// Return a list of containers where the specified AAD principal has data plane access.
		/// </summary>
		/// <param name="account">The storage account for which to retrieve container access.</param>
		/// <param name="principalId">The AAD principal for which to retrieve role assignments.</param>
		/// <returns>An List<ContainerRole>.</returns>
		public IList<ContainerRole> GetContainerRoleAssignments(string account, string principalId)
		{
			VerifyToken();

			// Get Storage Account Resource ID
			var accountResourceId = GetAccountResourceId(account);

			// Get Auth Management Client
			var amClient = new AuthorizationManagementClient(_tokenCredentials);

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

		#endregion

		#region Private Methods

		private void VerifyToken()
		{
			if (_tokenCredentials != null)
				return;

			if (_accessToken.Token == null || _accessToken.ExpiresOn < DateTime.Now)
			{
				var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
				_accessToken = new DefaultAzureCredential().GetToken(tokenRequestContext);
			}

			_tokenCredentials = new Rest.TokenCredentials(_accessToken.Token);
		}

		private RoleAssignment AddRoleAssignment(string scope, string roleName, string principalId)
		{
			VerifyToken();

			var amClient = new AuthorizationManagementClient(_tokenCredentials);
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

		private IList<StorageDataPlaneRole> GetStorageDataPlaneRolesByScope(string scope, string principalId = null)
		{
			VerifyToken();

			// Get Auth Management Client
			var amClient = new AuthorizationManagementClient(_tokenCredentials);

			// Find all the applicable built-in role definition IDs that would give a
			// principal access to storage account data plane
			// TODO: Abstract into VerifyRoleCredentials() method
			if (roleDefinitions == null)
			{
				roleDefinitions = amClient.RoleDefinitions.List(scope)
					.Where(rd => rd.RoleType.Equals("BuiltInRole", StringComparison.OrdinalIgnoreCase)
							  && rd.RoleName.StartsWith("Storage Blob Data")
					).ToList();
			}

			/* Query the scope's role assignments.
			 * This will only return role assignments where the provided token has
			 * Microsoft.Authorization/roleAssignments/read authorization.
			 * For example, by granting the app registration User Access Administrator on storage accounts
			 * in the specified subscription, this call will return any role assignment granted on the storage accounts.
			 * NOTE: Storage-as-a-service does not currently support role assignments at levels higher than storage account.
			 * I.e., a storage data plane role assignment at the resource group level or higher will not be reflected correctly.
			 GET https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/{resourceProviderNamespace}
			 			/{parentResourcePath}/{resourceType}/{resourceName}
						?$filter={$filter}&api-version=2015-07-01
			 */
			IPage<RoleAssignment> res = null;
			try
			{
				// Filter Role Assignments by principal ID if specified (AAD object ID of the signed in user)
				ODataQuery<RoleAssignmentFilter> q = new();
				q.Filter = (principalId != null) ? $"assignedTo('{principalId}')" : "atScope()";
				res = amClient.RoleAssignments.ListForScope(scope, q);
			}
			catch (Exception ex)
			{
				log.LogError(ex, scope);
				return new List<StorageDataPlaneRole>() { new() { RoleName = "Error reading access" } };        // Return blank list
			}

			// Join Role Assignments and Role Definitions
			var storageDataPlaneRoles = res.Join(roleDefinitions, ra => ra.RoleDefinitionId, rd => rd.Id,
						(ra, rd) => new StorageDataPlaneRole()
						{
							RoleName = rd.RoleName,
							Scope = ra.Scope,
							PrincipalType = ra.PrincipalType,
							PrincipalId = ra.PrincipalId
						}).ToList();

			return storageDataPlaneRoles;
		}

		#endregion

		public class ContainerRole : StorageDataPlaneRole
		{
			public string Container { get; set; }
		}

		public class StorageDataPlaneRole
		{
			public string RoleName { get; set; }
			public string Scope { get; set; }
			public string PrincipalId { get; set; }
			public string PrincipalType { get; set; }
			public string Id { get; set; }
		}
	}
}
