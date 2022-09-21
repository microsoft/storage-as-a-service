using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccount
	{
		private string _friendlyName;
		public string StorageAccountName { get; set; }

		public string FriendlyName {
			get { return String.IsNullOrWhiteSpace(_friendlyName) ? StorageAccountName : _friendlyName; }
			set { _friendlyName = value; }
		}
	}
}
