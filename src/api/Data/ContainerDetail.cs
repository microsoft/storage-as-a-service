using System;
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Services
{
	public class ContainerDetail
	{
		public string Name { get; set; }
		public Uri StorageExplorerDirectLink { get; set; }
		public Dictionary<string, string> Metadata { get; internal set; }
		public IList<StorageRbacEntry> Access { get; set; }
	}
}
