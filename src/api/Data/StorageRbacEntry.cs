// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.UsEduCsu.Saas.Services;

public class StorageRbacEntry
{
	public string RoleName { get; set; }
	public string PrincipalName { get; set; }
	public string PrincipalId { get; set; }
	public int Order { get; set; }
	public bool IsInherited { get; set; }
	public string RoleAssignmentId { get; set; }
}