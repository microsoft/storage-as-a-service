// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.UsEduCsu.Saas.Services;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas;

public static class FileSystems
{
	[ProducesResponseType(typeof(FileSystemDetail), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[FunctionName("FileSystemsContainer")]
	public static IActionResult GetContainer(
		[HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "FileSystems/{account}/{container}")]
		HttpRequest req,
		ILogger log, string account, string container)
	{
		if (!Configuration.ValidateSharedKey(req, Configuration.ApiKey.FileSystems))
		{
			// TODO: Log
			return new UnauthorizedResult();
		}

		if (Services.Extensions.AnyNullOrEmpty(account, container))
		{
			// TODO: log
			return new BadRequestResult();
		}

		// Limit total tries to 2 (retry = 1)
		// Default retries are 5, which can take 5+ seconds to return for a non-existing storage account
		DataLakeClientOptions opts = new();
		opts.Retry.MaxRetries = 1;
		FileSystemOperations fso = new(log, new DefaultAzureCredential(), account, opts);

		var detail = fso.GetFileSystemDetail(container);

		return detail is not null
			? new OkObjectResult(detail)
			: new NotFoundObjectResult("Container or storage account doesn't exist");
	}

	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[FunctionName("AuthorizationDelete")]
	public static IActionResult AuthorizationDelete(
		[HttpTrigger(AuthorizationLevel.Anonymous, "DELETE", Route = "FileSystems/{account}/{container}/authorization/{rbacId}")]
		HttpRequest req, ILogger log, string account, string container, string rbacId)
	{
		// Verify Parameter
		if (Services.Extensions.AnyNullOrEmpty(account, container, rbacId))
		{
			return new BadRequestResult();
		}

		if (req == null)
			log.LogError("err");

		// Validate Authorized Principal
		ClaimsPrincipalResult cpr = new(UserOperations.GetClaimsPrincipal(req));

		if (!cpr.IsValid)
		{
			log.LogWarning("No valid ClaimsPrincipal found in the request: '{0}'", cpr.Message);
			return new UnauthorizedResult();
		}

		var principalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);

		// Get Role Operations Setup
		var roleOps = new RoleOperations(log);

		// Verify user can delete RBAC
		ResourceGraphOperations rgo = new(log, new DefaultAzureCredential());
		string accountResourceId = rgo.GetAccountResourceId(account);
		var canModifyRbac = CanModifyRbac(roleOps, accountResourceId, container, principalId);
		if (!canModifyRbac)
			return new UnauthorizedResult();

		// Submit Role Authorization Request
		var roleAssignment = roleOps.DeleteRole(account, container, rbacId);
		if (roleAssignment == null)
			return new NotFoundResult();

		// Http Accepted 202
		return new AcceptedResult();
	}


	[ProducesResponseType(typeof(StorageRbacEntry), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[FunctionName("AuthorizationCreate")]
	public static IActionResult AuthorizationCreate(
		[HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "FileSystems/{account}/{container}/authorization")]
		HttpRequest req, ILogger log, string account, string container)
	{
		// Validate Parameters
		if (Services.Extensions.AnyNullOrEmpty(account, container))
		{
			return new BadRequestResult();
		}

		// Validate Authorized Principal
		ClaimsPrincipalResult cpr = new(UserOperations.GetClaimsPrincipal(req));
		if (!cpr.IsValid)
		{
			log.LogWarning("No valid ClaimsPrincipal found in the request: '{0}'", cpr.Message);
			return new UnauthorizedResult();
		}
		var ownerPrincipalId = UserOperations.GetUserPrincipalId(cpr.ClaimsPrincipal);

		// Get Role Operations Setup
		var roleOps = new RoleOperations(log);

		// Verify user can delete RBAC
		ResourceGraphOperations rgo = new(log, new DefaultAzureCredential());
		string accountResourceId = rgo.GetAccountResourceId(account);
		var canModifyRbac = CanModifyRbac(roleOps, accountResourceId, container, ownerPrincipalId);
		if (!canModifyRbac)
			return new UnauthorizedResult();

		// Request body is supposed to contain the user's identity claim
		var rbac = (req.Body.Length > 0)
			? JsonSerializer.Deserialize<AuthorizationRequest>(req.Body)
			: new AuthorizationRequest();
		if (Services.Extensions.AnyNullOrEmpty(rbac.Identity, rbac.Role))
			return new BadRequestResult();

		// Convert Identity into principal ID
		var mgo = new MicrosoftGraphOperations(log, new DefaultAzureCredential());
		var targetPrincipalId = mgo.GetObjectId( rbac.Identity);
		if (Services.Extensions.AnyNullOrEmpty(targetPrincipalId))
			return new BadRequestResult();

		// Convert Shortened Rolls to Full Names
		if (!rbac.Role.Contains("Storage Blob Data"))
			rbac.role = $"Storage Blob Data {rbac.Role}";

		// Submit Role Authorization Request
		var roleAssignment = roleOps.AssignRole(account, container, rbac.Role, targetPrincipalId);
		if (roleAssignment == null)
			return new NotFoundResult();

		// Get Principal Name
		var principalName = mgo.GetDisplayName(targetPrincipalId);

		// Convert to Storage Entry
		var storageRbacEntry = new StorageRbacEntry()
		{
			PrincipalId = roleAssignment.PrincipalId,
			PrincipalName = principalName,
			RoleName = roleAssignment.RoleName.Replace("Storage Blob Data ", string.Empty),
			RoleAssignmentId = roleAssignment.RoleAssignmentId[(roleAssignment.RoleAssignmentId.LastIndexOf('/') + 1)..],
			IsInherited = false,
			Order = 0
		};

		return new OkObjectResult(storageRbacEntry);
	}

	private static bool CanModifyRbac(RoleOperations roleOps, string accountResourceId, string container, string principalId)
	{
		// Determine Access Roles
		// TODO: Optimization opportunity: Retrieve the role assignments for the account once, and then only the assignments at the container scope
		var roles = roleOps.GetStorageDataPlaneRoleAssignments(accountResourceId, container);
		var canModifyRbac = roles
			.Any(r => r.PrincipalId == principalId && r.RoleName == "Owner");

		return canModifyRbac;
	}

	// TODO:Convert from camelCase to ProperCase during serialization
	public class AuthorizationRequest
	{
		public string identity { get; set; }
		public string role { get; set; }

		public string Identity {
			get => identity;
		}
		public string Role {
			get => role;
		}
	}
}
