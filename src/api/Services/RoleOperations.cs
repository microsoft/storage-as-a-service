using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Management.Authorization;
using Microsoft.Azure.Management.Authorization.Models;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class RoleOperations
	{
		// https://blogs.aaddevsup.xyz/2020/05/using-azure-management-libraries-for-net-to-manage-azure-ad-users-groups-and-rbac-role-assignments/

		private readonly ILogger log;
		private TokenCredentials tokenCredentials;

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
			if (roleAssignment == null)
			{
				var racp = new RoleAssignmentCreateParameters(roleDefinition.Id, principalId);
				var roleAssignmentId = Guid.NewGuid().ToString();
				roleAssignment = amClient.RoleAssignments.Create(scope, roleAssignmentId, racp);
			}
			return roleAssignment;
		}

		public void AssignRoles(string account, string container, string ownerId)
		{
			try
			{
				// Get Storage Account Resource ID
				var accountResourceId = GetAccountResourceId(account);

				// Create Role Assignments
				string containerScope = $"{accountResourceId}/blobServices/default/containers/{container}";

				// Allow user to manage ACL for container
				var ra = AddRoleAssignment(containerScope, "Storage Blob Data Owner", ownerId);
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				Console.WriteLine(ex.Message);
				throw;
			}
		}

		/// <summary>
		/// Return a list of containers where the specified AAD principal has data plan access.
		/// </summary>
		/// <param name="account">The storage account for which to retrieve container access.</param>
		/// <param name="principalId">The AAD principal for which to retrieve role assignments.</param>
		/// <returns>An IList<ContainerRole>.</returns>
		public IList<ContainerRole> GetContainerRoleAssignments(string account, string principalId)
		{
			return GetContainerRoleAssignments(account).Where( ra => ra.PrincipalId == principalId).ToList();
		}

		internal IEnumerable<ContainerRole> GetContainerRoleAssignments(string account)
		{
			// TODO: Consider creating a TokenCredentialManager
			// (local var) TokenCredentials tc = TokenCredentialManager.GetToken();
			VerifyToken();

			// Get Storage Account Resource ID
			var accountResourceId = GetAccountResourceId(account);

			// TODO: Optimize for cache and performance. Too many calls.

			var amClient = new AuthorizationManagementClient(tokenCredentials);

			// Find all the applicable built-in role definition IDs that would give a principal access to storage account data plane
			var roleDefinitions = amClient.RoleDefinitions.List(accountResourceId)
				.Where(rd => rd.RoleName.StartsWith("Storage Blob"));

			// Create an IList<string> of the role definition IDs to use in the next LINQ
			var roleDefinitionIds = roleDefinitions
				.Select(rd => rd.Id);

			// Retrieve the applicable role assignments scoped to containers for the specified AAD principal
			var roleAssignments = amClient.RoleAssignments.ListForScope(accountResourceId)
				.Where(ra => ra.Scope.Contains("/blobServices/default/containers/")
					&& roleDefinitionIds.Contains(ra.RoleDefinitionId))
				// Transform matching role assignments into the method's return value
				.Select(ra => new ContainerRole()
				{
					RoleName = roleDefinitions.Single(rd => rd.Id.Equals(ra.RoleDefinitionId)).RoleName,
					Container = ra.Scope.Split('/').Last(),
					PrincipalId = ra.PrincipalId
				});

			return roleAssignments;
		}

		public class ContainerRole
		{
			public string RoleName { get; set; }
			public string Container { get; set; }
			public string PrincipalId {get;set;}
		}
	}
}
