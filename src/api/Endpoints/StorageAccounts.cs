// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using static Microsoft.UsEduCsu.Saas.FileSystems;

namespace Microsoft.UsEduCsu.Saas;

public static class StorageAccounts
{
	[ProducesResponseType(typeof(IList<ContainerDetail>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[FunctionName("StorageAccounts")]
	public static IActionResult Get(
		[HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "StorageAccounts/{account?}")] HttpRequest req,
		ILogger log, string account)
	{
		// Validate Authorized Principal
		ClaimsPrincipalResult cpr = new(UserOperations.GetClaimsPrincipal(req));

		if (!cpr.IsValid)
		{
			log.LogWarning("No valid ClaimsPrincipal found in the request: '{0}'", cpr.Message);
			return new UnauthorizedResult();
		}

		var principalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);

		// List of Storage Accounts or List of Containers for a storage account
		if (string.IsNullOrEmpty(account))
		{
			StorageAccountOperations sao = new(log);
			var result = sao.GetAccessibleStorageAccounts(principalId);             // TODO: Check for refresh parameter
			return new OkObjectResult(result);
		}
		else
		{
			var containerDetails = PopulateContainerDetail(account, principalId, log);
			//TODO: camelCasing - string json = JsonSerializer.Serialize<List<ContainerDetail>>(containerDetails, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			return new OkObjectResult(containerDetails);
		}
	}

	private static List<ContainerDetail> PopulateContainerDetail(string account, string principalId, ILogger log)
	{
		// TODO: Move to FileSystemOperations

		// Get Storage Account Uri
		var storageUri = Configuration.GetStorageUri(account);

		// Get Storage Account Client
		var ApiCredential = new DefaultAzureCredential();
		var storageAccountClient = new DataLakeServiceClient(storageUri, ApiCredential);

		// Setup the Role Operations
		var roleOperations = new RoleOperations(log);

		// Get Container List
		var accessibleContainers = new StorageAccountOperations(log)
			.GetAccessibleContainerDetails(principalId, account);

		// User Operations
		var graphOps = new MicrosoftGraphOperations(log, ApiCredential);

		// Initilize the result
		ConcurrentBag<ContainerDetail> containerDetails = new();

		// Need to get the filesytems
		try
		{
			var filesystems = storageAccountClient.GetFileSystems(FileSystemTraits.Metadata)
					.Where(c => accessibleContainers.Contains(c.Name))  // Filter for the future
					.Select(l => new { l.Name, l.Properties })
					.ToList();

			ResourceGraphOperations rgo = new(log, ApiCredential);
			string accountResourceId = rgo.GetAccountResourceId(account);

			// Build additional details
			Parallel.ForEach(filesystems, (fs) =>
			{
				var cd = GetContainerDetail(roleOperations, graphOps, account, accountResourceId,
					fs.Name, fs.Properties);
				containerDetails.Add(cd);
			});
		}
		catch (Exception ex)
		{
			log.LogError(ex, ex.Message);
		}

		// Return result
		return containerDetails
			.OrderBy(s => s.Name) // using Extension method, but outside of concurrent thread
			.ThenBy(c => c.Name)
			.ToList();
	}

	private static ContainerDetail GetContainerDetail(RoleOperations roleOps, MicrosoftGraphOperations graphOps,
		string account, string accountResourceId, string container, FileSystemProperties properties)
	{
		// TODO: Move to FileSystemOperations

		// Calculate Cost Per TB
		decimal costPerTB = 0.0M;
		if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COST_PER_TB")))
			_ = decimal.TryParse(Environment.GetEnvironmentVariable("COST_PER_TB"), out costPerTB);

		// Get Metadata information
		var metadata = (properties.Metadata ?? new Dictionary<string, string>());
		metadata["LastModified"] = properties.LastModified.ToString("G", CultureInfo.CurrentCulture);

		// Rbac Principal Types to Display
		var validTypes = new[] { "Group", "User" };
		var sortOrderMap = new Dictionary<string, int>() {
			{ "Storage Blob Data Owner", 1 },
			{ "Storage Blob Data Contributor", 2 },
			{ "Storage Blob Data Reader", 3 } };

		// Determine Access Roles
		// TODO: Optimization opportunity: Retrieve the role assignments for the account once, and then only the assignments at the container scope
		var roles = roleOps.GetStorageDataPlaneRoleAssignments(accountResourceId, container);
		var rbacEntries = roles
			.Where(r => validTypes.Contains(r.PrincipalType))       // Only display User and Groups (no Service Principals)
			.Select(r => new StorageRbacEntry()
			{
				RoleName = r.RoleName.Replace("Storage Blob Data ", string.Empty),
				PrincipalId = r.PrincipalId,
				PrincipalName = graphOps.GetDisplayName(r.PrincipalId),
				Order = sortOrderMap.GetValueOrDefault(r.RoleName),
				IsInherited = r.IsInherited,
				RoleAssignmentId = r.RoleAssignmentId
			})
			.OrderBy(r => r.Order).ThenBy(r => r.PrincipalName
			).ToList();

		// Package in ContainerDetail
		var uri = Configuration.GetStorageUri(account, container).ToString();
		var cd = new ContainerDetail()
		{
			Name = container,
			Metadata = metadata,
			Access = rbacEntries,
			StorageExplorerDirectLink = $"storageexplorer://?v=2&tenantId={Configuration.TenantId}&type=fileSystem&container={container}&serviceEndpoint={HttpUtility.UrlEncode(uri)}",
			Uri = uri
		};

		return cd;
	}
}
