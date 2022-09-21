// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccountAndContainers : IEquatable<StorageAccountAndContainers>
	{
		public string StorageAccountName { get; set; }

		public List<string> Containers { get; set; } = new List<string>();

		public bool AllContainers { get; set; }     // Principal has read acces on storage account & list is All containers

		public override bool Equals(object obj)
		{
			return Equals(obj as StorageAccountAndContainers);
		}
		public override int GetHashCode()
		{
			return HashCode.Combine(this.StorageAccountName, this.Containers, this.AllContainers);
		}

		public bool Equals(StorageAccountAndContainers other)
		{
			if (other == null) return false;
			if (this.StorageAccountName != other.StorageAccountName)
				return false;

			if (!this.Containers.Equals(other.Containers))
				return false;

			if (this.AllContainers != other.AllContainers)
				return false;

			return true;
		}
	}
}
