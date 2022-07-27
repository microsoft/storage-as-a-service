using System;
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccountAndContainers : IEquatable<StorageAccountAndContainers>
	{
		public string StorageAccountName { get; set; }

		public List<string> Containers { get; set; } = new List<string>();

		public bool AllContainers { get; set; }     // Principal has read acces on storage account & list is All containers

		public bool Equals(StorageAccountAndContainers other)
		{
			if (this.StorageAccountName != other.StorageAccountName)
				return false;

			if (this.Containers != other.Containers)
				return false;

			if (this.AllContainers != other.AllContainers)
				return false;

			return true;
		}
	}
}
