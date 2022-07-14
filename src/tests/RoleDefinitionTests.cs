using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Files.DataLake;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.UsEduCsu.Saas.Tests
{
	public class RoleDefinitionTests
	{
		private readonly ILogger log = new LoggerFactory().CreateLogger<RoleDefinitionTests>();

		public RoleDefinitionTests()
		{
			ConfigureEnvironmentVariablesFromLocalSettings();
		}

		[Fact]
		public void DeleteOrphanedRoleAssignments()
		{
			RoleOperations ro = new (log);
			var x  = ro.GetContainerRoleAssignments("stsaasdemoeastus0202","c2a89c81-38a2-49de-a02b-0e8b90e56745");

			foreach(var y in x)
				ro.DeleteRoleAssignment(y.Id);

			Assert.True(x.Count > 0);

		}

		static void ConfigureEnvironmentVariablesFromLocalSettings()
		{
			var path = Environment.CurrentDirectory;
			var json = File.ReadAllText(Path.Join(path, "local.settings.json"));
			var parsed = Newtonsoft.Json.Linq.JObject.Parse(json).Value<Newtonsoft.Json.Linq.JObject>("Values");

			foreach (var item in parsed)
			{
				Environment.SetEnvironmentVariable(item.Key, item.Value.ToString());
			}
		}
	}
}
