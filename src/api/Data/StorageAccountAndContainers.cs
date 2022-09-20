// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccountAndContainers : IEquatable<StorageAccountAndContainers>
	{

		public StorageAccount Account { get; set; }

		public List<string> Containers { get; set; } = new List<string>();

		public bool AllContainers { get; set; }     // Principal has read acces on storage account & list is All containers

		public bool Equals(StorageAccountAndContainers other)
		{
			if (this.Account.StorageAccountName != other.Account.StorageAccountName)
				return false;

			if (this.Containers != other.Containers)
				return false;

			if (this.AllContainers != other.AllContainers)
				return false;

			return true;
		}

		public StorageAccountAndContainers() {
			Account = new StorageAccount();
		}
	}
}
