// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.UsEduCsu.Saas.Tests;

public class FileSystemTests
{
	//private readonly ILogger log = new LoggerFactory().CreateLogger<FileSystemTests>();

	public FileSystemTests()
	{
		ConfigureEnvironmentVariablesFromLocalSettings();
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
