using Microsoft.UsEduCsu.Saas.Services;
using System;
using Xunit;

namespace Microsoft.UsEduCsu.Saas.Tests
{
	public class ConfigurationTests
	{
		[Fact]
		public void GetStorageUri_Simple_Test()
		{
			Assert.Equal("https://accountname.dfs.core.windows.net/",
				SasConfiguration.GetStorageUri("accountname").ToString());
		}

		[Fact]
		public void GetStorageUri_WithFileSystem_Test()
		{
			Assert.Equal("https://accountname.dfs.core.windows.net/container",
				SasConfiguration.GetStorageUri("accountname", "container").ToString());
		}

		[Fact]
		public void GetStorageUri_InvalidAccount_Test()
		{
			UriFormatException ex = Assert.ThrowsAny<UriFormatException>(
				() => SasConfiguration.GetStorageUri(string.Empty));
			Assert.Contains("hostname could not be parsed", ex.Message);
		}
	}
}
