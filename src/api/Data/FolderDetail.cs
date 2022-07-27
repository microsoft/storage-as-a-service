using System.Collections.Generic;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class FileSystemDetail
	{
		public string Name { get; set; }
		public string LastModified { get; set; }
		public string Size { get; set; }        // Not Calculated
		public string Cost { get; set; }
		public string FundCode { get; set; }    // Metadata
		public string Owner { get; set; }       // Metadata
		public string URI { get; set; }
		public IList<string> UserAccess { get; set; }
		public string StorageExplorerURI { get; set; }
	}
}
