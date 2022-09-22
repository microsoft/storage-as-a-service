// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Data;

internal class StorageAccountAndContainers : IEquatable<StorageAccountAndContainers>
{

	public StorageAccount Account { get; set; } = new();

	public List<string> Containers { get; set; } = new List<string>();

public bool AllContainers { get; set; }     // Principal has read acces on storage account & list is All containers

	#region IEquatable implementation

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
			return false;
			return false;

	if (!this.Containers.Equals(other.Containers))
		return false;
			return true;
		}

	}
		return true;
	}

	#endregion
		return true;
	}

}
