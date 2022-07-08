using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccountAndContainers
	{
		public string StorageAccountName { get; set; }

		public IList<string> Containers { get; internal set; } = new List<string>();

		public bool AllContainers { get; set; }
	}
}
