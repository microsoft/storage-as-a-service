// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Services;

public class ContainerDetail
{
	public string Name { get; set; }
	public string Uri { get; set; }
	public string StorageExplorerDirectLink { get; set; }
	public IDictionary<string, string> Metadata { get; internal set; }
	public IList<StorageRbacEntry> Access { get; set; }
}
