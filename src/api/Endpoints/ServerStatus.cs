// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas;

public static class ServerStatus
{
	[ProducesResponseType(typeof(Status), StatusCodes.Status200OK)]
	[FunctionName("ServerStatus")]
#pragma warning disable IDE0060 // Remove unused parameter (req, log)
	public static IActionResult Get(
		[HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "ServerStatus")] HttpRequest req,
		ILogger log)
#pragma warning restore IDE0060 // Remove unused parameter
	{
		// Validate Configuration
		var (isConfigValid, errors) = Configuration.Validate();

		// Prepare respone with errors if exist
		var status = new Status()
		{
			Message = string.Join(", ", errors.Select(d => $"{d.Key}: {d.Value}")),
			Errors = errors
		};

		// Return result
		return new OkObjectResult(status);
	}

	public class Status
	{
		public string Message { get; set; }
		public Dictionary<string, string> Errors { get; set; }
	}
}
