using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Microsoft.UsEduCsu.Saas.Data
{
	internal class StorageAccount
	{
		private string _friendlyName;
		private TextInfo _textInfo = new CultureInfo("en-US",false).TextInfo;
		public string StorageAccountName { get; set; }

		public string FriendlyName {
			get { return String.IsNullOrWhiteSpace(_friendlyName) ? _textInfo.ToTitleCase(StorageAccountName) : _textInfo.ToTitleCase(_friendlyName); }
			set { _friendlyName = value; }
		}
	}
}
