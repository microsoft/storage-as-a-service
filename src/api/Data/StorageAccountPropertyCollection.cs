using System;
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccountPropertyCollection
	{
		public StorageAccountPropertyCollection()
		{
			Value = new List<StorageAccountProperty>();
		}
		public List<StorageAccountProperty> Value { get; set; }
	}

	internal class StorageAccountProperty
	{
		public string StorageAccountName { get; set; }
		public string StorageAccountFriendlyName { get; set; }
	}
}