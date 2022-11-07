// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Management.Authorization;
using Microsoft.Azure.Management.Authorization.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.Azure.OData;
using Microsoft.UsEduCsu.Saas.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas.Services;

internal sealed class RoleOperations : IDisposable
{
	// https://blogs.aaddevsup.xyz/2020/05/using-azure-management-libraries-for-net-to-manage-azure-ad-users-groups-and-rbac-role-assignments/

	private readonly ILogger log;
	private Rest.TokenCredentials _tokenCredentials;
	private AccessToken _accessToken;
	private AuthorizationManagementClient _amClient;
	private readonly CacheHelper _cache;
	private bool disposedValue;

	private AuthorizationManagementClient AuthMgtClient
	{
		get
		{
			_amClient ??= new AuthorizationManagementClient(TokenCredentials);
			return _amClient;
		}
	}

	// Caches the list of storage plane data role definitions
	private static ConcurrentDictionary<string, IList<RoleDefinition>> roleDefinitions = new();

	// Lock objects for thread-safety
	private readonly object tokenCredentialsLock = new();

	public RoleOperations(ILogger log)
	{
		this.log = log;
		_cache = CacheHelper.GetRedisCacheHelper(log);
	}

	#region Internal Methods

	internal RoleAssignment AssignRole(string accountResourceId, string container, string role, string principalId)
	{
		try
		{
			// Create container resource ID
			string containerScope = $"{accountResourceId}/blobServices/default/containers/{container}";

			var roleAssignment = AddRoleAssignment(containerScope, role, principalId);

			return roleAssignment;
		}
		catch (Exception ex)
		{
			log.LogError(ex, ex.Message);
			return null;
		}
	}

	internal RoleAssignment DeleteRoleAssignment(string accountResourceId, string container, string rbacId)
	{
		try
		{
			// Get the container resource ID
			var containerResourceId = $"{accountResourceId}/blobServices/default/containers/{container}";

			// Find the role assignment to be deleted
			var roleAssignments = GetRoleAssignments(containerResourceId);
			var authRoleAssignment = roleAssignments.FirstOrDefault(ra => ra.Id.EndsWith($"/{rbacId}", StringComparison.OrdinalIgnoreCase));
			if (authRoleAssignment is null)
				return null;

			// Try to delete
			authRoleAssignment = AuthMgtClient.RoleAssignments.DeleteById(authRoleAssignment.Id);

			// Convert to Internal Role Assignment
			var roleAssignment = new RoleAssignment(authRoleAssignment, null, false);

			return roleAssignment;
		}
		catch (Exception ex)
		{
			log.LogError(ex, ex.Message);
			return null;
		}
	}

	internal bool CanModifyRbac(string accountResourceId, string container, string principalId)
	{
		// Determine Access Roles
		// TODO: This might not work if role assignment is via group membership
		var roleAssignments = GetStorageDataPlaneRoleAssignments(accountResourceId, container);

		var canModifyRbac = roleAssignments
			.Any(ra => CanModifyRbac(ra, principalId));

		return canModifyRbac;
	}

	/// <summary>
	/// Determines whether the specified principal ID can modify RBAC assignments
	/// based on the specified role assignment.
	/// </summary>
	/// <param name="ra">The role assignment to verify against.</param>
	/// <param name="principalId">The principal to verify.</param>
	/// <returns></returns>
	/// <remarks>This method is meant to be called from a lambda processing a enumerable of role assignments.</remarks>
	internal bool CanModifyRbac(RoleAssignment ra, string principalId)
	{
		return ra.RoleName.Equals("Storage Blob Data Owner", StringComparison.OrdinalIgnoreCase)
			&& ra.PrincipalId.Equals(principalId, StringComparison.OrdinalIgnoreCase);
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

		// Get Application level Credentials
		var appCred = new DefaultAzureCredential();

		// Get All Storage Roles for a user across subscriptions
		IList<RoleAssignment> roleAssignments = GetAllStorageDataPlaneRoleAssignments(principalId);

		// TODO: Unit test for pattern
		const string ScopePattern = @"^/subscriptions/[0-9a-f-]{36}/resourceGroups/[\w_\.-]{1,90}/providers/Microsoft.Storage/storageAccounts/(?<accountName>\w{3,24})(/blobServices/default/containers/(?<containerName>[\w-]{3,63}))?$";
		Regex re = new(ScopePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);          // TODO: Consider having class-level (static) scoped compiled Regex instance

		List<StorageAccountAndContainers> results = new();

		// Create RGO object to get Azure tag information
		var rgo = new ResourceGraphOperations(log, TokenCredentials);

		// Get the existing storage account properties from the cache. If there is no cached entry, create a new one.
		var storageAccountProperties = _cache.GetStorageAccountProperties();

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
				.SingleOrDefault(x => x.Account.StorageAccountName.Equals(storageAccountName, StringComparison.OrdinalIgnoreCase));

			// If this is the first time we've encountered this storage account, Set the storage account name property and add to result set
			if (fsr == null)
			{
				// Check for cached storage account properties for the storage account name
				var val = storageAccountProperties?.Value.FirstOrDefault(x => x.StorageAccountName == storageAccountName);

				// Check for the friendly name in the cache. If it doesn't exist, get it from Azure and add it to the cache.
				var fname = val?.FriendlyName;
				if (val is null)
				{
					// Get the friendly name from Azure
					// If there is no friendly name tag specified in settings configuration, this will end up displaying the storage account name instead using the property definition
					fname = rgo.GetAccountResourceTagValue(storageAccountName, Configuration.StorageAccountFriendlyTagNameKey);
					// Save back into cache
					storageAccountProperties.Value.Add(new StorageAccount
					{
						StorageAccountName = storageAccountName,
						FriendlyName = fname
					});
				}

				fsr = new StorageAccountAndContainers();
				fsr.Account.StorageAccountName = storageAccountName;
				fsr.Account.FriendlyName = fname;
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
					var adls = new FileSystemOperations(log, appCred, fsr.Account.StorageAccountName);
					var containers = adls.GetContainers();                      // Access is to entire storage account; retrieve all containers
					fsr.Containers = containers.Select(fs => fs.Name).ToList();     // Replace any previously included containers
					fsr.AllContainers = true;                       // There can't be any more containers in this storage account
				}
			}
		}

		// Update the account properties cache
		if (storageAccountProperties is not null)
			_cache.SetStorageAccountProperties(storageAccountProperties);

		return results;
	}

	/// <summary>
	/// Retrieves all Azure Storage data plane roles (Storage Blob Data *) assignments
	/// for the specified blob container.
	/// </summary>
	/// <param name="accountResourceId">The Azure resource ID of the storage account.</param>
	/// <param name="container">A container name in the storage account.</param>
	/// <returns>The list of role assignments.</returns>
	internal IList<RoleAssignment> GetStorageDataPlaneRoleAssignments(string accountResourceId, string container)
	{
		// /subscriptions/[subscription id]/resourceGroups/[resource group name]/providers/Microsoft.Storage/storageAccounts/[storage account]/blobServices/default/containers/[container name]
		var scope = (container is null)
						? accountResourceId
						: $"{accountResourceId}/blobServices/default/containers/{container}";

		return GetStorageDataPlaneRolesByScope(scope);
	}

	#endregion

	#region Private Methods

	private Rest.TokenCredentials TokenCredentials
	{
		get
		{
			// TODO: Why is this not done in the constructor?

			if (_tokenCredentials is null)
			{
				lock (tokenCredentialsLock)
				{
					if (_accessToken.Token is null
						|| _accessToken.ExpiresOn < DateTime.Now)
					{
						var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
						_accessToken = new DefaultAzureCredential().GetToken(tokenRequestContext);
					}

					_tokenCredentials = new Rest.TokenCredentials(_accessToken.Token);
				}
			}

			return _tokenCredentials;
		}
	}

	/// <summary>
	/// Retrieves the storage data plane role definitions for the specified scope.
	/// </summary>
	/// <param name="resourceId">The Azure resource ID of the scope to retrieve role definitions for.</param>
	/// <returns>The storage data plane role definitions for the subscription of the specified Azure resource ID.</returns>
	private IList<RoleDefinition> GetRoleDefinitions(string resourceId)
	{
		string subscriptionId = ResourceGraphOperations.ExtractSubscriptionIdFromScope(resourceId);

		if (!string.IsNullOrEmpty(subscriptionId))
		{
			IList<RoleDefinition> ScopedRoleDefinitions;

			// If the role definitions for this subscription haven't been retrieved yet
			if (!roleDefinitions.ContainsKey(subscriptionId))
			{
				ScopedRoleDefinitions = AuthMgtClient.RoleDefinitions.List(resourceId)
					.Where(rd => rd.RoleName.StartsWith("Storage Blob Data", StringComparison.Ordinal)
							&& rd.RoleType.Equals("BuiltInRole", StringComparison.OrdinalIgnoreCase))
					.ToList();

				_ = roleDefinitions.TryAdd(subscriptionId, ScopedRoleDefinitions);
			}
			else
			{
				ScopedRoleDefinitions = roleDefinitions[subscriptionId];
			}

			return ScopedRoleDefinitions;
		}

		log.LogError("Could not parse resource ID '{0}' to extract a subscription ID.", resourceId);
		return new List<RoleDefinition>();
	}

	private RoleAssignment AddRoleAssignment(string scope, string roleName, string principalId)
	{
		var ScopedRoleDefinitions = GetRoleDefinitions(scope);

		// Get the specific role definition by name
		var roleDefinition = ScopedRoleDefinitions
			.FirstOrDefault(x => x.RoleName == roleName);

		if (roleDefinition is not null)
		{
			// Get Current Role Assignments
			var roleAssignments = GetRoleAssignments(scope, principalId);

			// Filter down to the specific role definition
			var authRoleAssignment = roleAssignments?.FirstOrDefault(ra => ra.PrincipalId == principalId
														&& ra.RoleDefinitionId == roleDefinition.Id);

			// Create New Role Assignment
			if (authRoleAssignment is null)
			{
				var racp = new RoleAssignmentCreateParameters(roleDefinition.Id, principalId);
				var roleAssignmentId = Guid.NewGuid().ToString();
				authRoleAssignment = AuthMgtClient.RoleAssignments.Create(scope, roleAssignmentId, racp);

				// Convert to Internal Role Assignment
				var roleAssignment = new RoleAssignment(authRoleAssignment, roleDefinition, false);

				return roleAssignment;
			}
			else
			{
				// This role assignment already exists (but could be inherited)
				// Should create a return status to indicate that
			}
		}

		return null;
	}

	/// <summary>
	/// Retrieves storage data plane role assignments for the specified scope and optional principal.
	/// </summary>
	/// <param name="scope">The Azure resource ID for which to retrieve role assignments.</param>
	/// <param name="principalId">(optional) The AAD object ID of the principal to retrieve assignments for.</param>
	/// <returns>The list of storage data plane role assignments.</returns>
	private IList<RoleAssignment> GetStorageDataPlaneRolesByScope(string scope, string principalId = null)
	{
		var ScopedRoleDefinitions = GetRoleDefinitions(scope);

		// Get the Role Assignments for the scope
		IList<Azure.Management.Authorization.Models.RoleAssignment> assignments = GetRoleAssignments(scope, principalId);

		if (assignments is not null)
		{
			// Join Role Assignments and Role Definitions
			var storageDataPlaneRoles = assignments.Join(ScopedRoleDefinitions, ra => ra.RoleDefinitionId, rd => rd.Id,
						(ra, rd) => new RoleAssignment(ra, rd, !ra.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase)))
						.ToList();

			return storageDataPlaneRoles;
		}

		return new List<RoleAssignment>();
	}

	private IList<Azure.Management.Authorization.Models.RoleAssignment> GetRoleAssignments(string scope, string principalId = null)
	{
		/* Query the scope's role assignments.
		 * This will only return role assignments where the provided token has Microsoft.Authorization/roleAssignments/read authorization.
		 * For example, by granting the app registration User Access Administrator on storage accounts
		 * in the specified subscription, this call will return any role assignment granted on the storage accounts.
		 GET https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/{resourceProviderNamespace}
					/{parentResourcePath}/{resourceType}/{resourceName}
					?$filter={$filter}&api-version=2015-07-01
		 */

		try
		{
			// Filter Role Assignments by principal ID if specified (AAD object ID of the signed in user)
			var q = new ODataQuery<RoleAssignmentFilter>()
			{
				Filter = (principalId != null) ? $"assignedTo('{principalId}')" : "atScope()",
			};

			IList<Azure.Management.Authorization.Models.RoleAssignment> res = AuthMgtClient.RoleAssignments
				.ListForScope(scope, q)
				.ToList();

			return res;
		}
		catch (Exception ex)
		{
			// TODO: Create a proper error message
			log.LogError(ex, scope);
			return null;
		}
	}

	private IList<RoleAssignment> GetAllStorageDataPlaneRoleAssignments(string principalId)
	{
		var subscriptions = Configuration.GetSubscriptions();
		// Use a thread-safe unordered collection
		var roleAssignments = new ConcurrentBag<RoleAssignment>();

		Parallel.ForEach(subscriptions, subscription =>
		{
			// GetStorageDataPlaneRoles will not return null
			var scope = $"/subscriptions/{subscription}/";
			var assignments = GetStorageDataPlaneRolesByScope(scope, principalId);  // TODO: Getting them out in order of storage account to make processing more efficient?
			log.LogTrace("RoleOperations.GetStoragePlaneDataRoles({0}, {1}) returned {2} role assignments.",
				subscription, principalId, assignments.Count);

			foreach (var ra in assignments)
			{
				roleAssignments.Add(ra);
			}
		});

		// ToList as an extension method is not known to be thread-safe
		// ToArray is a method defined in the ConcurrentBag class and is thread-safe
		return roleAssignments.ToArray().ToList();
	}

	#endregion

	internal class RoleAssignment
	{
		public string RoleName { get; set; }
		public string Scope { get; set; }
		public string PrincipalId { get; set; }
		public string PrincipalType { get; set; }
		public bool IsInherited { get; set; }
		public string RoleAssignmentId { get; set; }

		/// <summary>
		/// Creates a new Microsoft.UsEduCsu.Saas.RoleOperations.RoleAssignment object
		/// from the specified Azure.Management.Authorization.Models.RoleAssignment object.
		/// </summary>
		/// <param name="authRoleAssignment"></param>
		internal RoleAssignment(Azure.Management.Authorization.Models.RoleAssignment authRoleAssignment,
			Azure.Management.Authorization.Models.RoleDefinition roleDefinition,
			bool isInherited)
		{
			RoleAssignmentId = authRoleAssignment.Id[(authRoleAssignment.Id.LastIndexOf('/') + 1)..];
			RoleName = roleDefinition?.RoleName;
			PrincipalId = authRoleAssignment.PrincipalId;
			PrincipalType = authRoleAssignment.PrincipalType;
			Scope = authRoleAssignment.Scope;
			IsInherited = isInherited;
		}
	}

	#region Disposable Pattern

	private void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				_amClient.Dispose();
			}

			_amClient = null;
			roleDefinitions = null;

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	#endregion
}
