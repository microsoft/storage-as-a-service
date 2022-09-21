// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class FileSystemDetail
	{
		public string Name { get; set; }
		public string LastModified { get; set; }
		public string FundCode { get; set; }    // Metadata
		public string Owner { get; set; }       // TODO: Determine if this is a Key
		public string URI { get; set; }
		public IList<string> UserAccess { get; set; }
		public string StorageExplorerURI { get; set; }
	}
}
