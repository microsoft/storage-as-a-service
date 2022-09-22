// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;

namespace Microsoft.UsEduCsu.Saas;

public static class FileSystems
{
	[ProducesResponseType(typeof(FileSystemDetail), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[FunctionName("FileSystemsContainer")]
	public static IActionResult GetContainer(
		[HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "FileSystems/{account}/{container}")]
		HttpRequest req,
		ILogger log, string account, string container)
	{
		if (!Configuration.ValidateSharedKey(req, Configuration.ApiKey.FileSystems))
		{
			// TODO: Log
			return new UnauthorizedResult();
		}

		if (Services.Extensions.AnyNullOrEmpty(account, container))
		{
			// TODO: log
			return new BadRequestResult();
		}

		// Limit total tries to 2 (retry = 1)
		// Default retries are 5, which can take 5+ seconds to return for a non-existing storage account
		DataLakeClientOptions opts = new();
		opts.Retry.MaxRetries = 1;
		FileSystemOperations fso = new(log, new DefaultAzureCredential(), account, opts);

		var detail = fso.GetFileSystemDetail(container);

		return detail is not null
			? new OkObjectResult(detail)
			: new NotFoundObjectResult("Container or storage account doesn't exist");
	}
}
