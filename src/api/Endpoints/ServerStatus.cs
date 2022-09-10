using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas
{
	public static class ServerStatus
	{
		[ProducesResponseType(typeof(ServerStatus.Status), StatusCodes.Status200OK)]
		[FunctionName("ServerStatus")]
		public static IActionResult Get(
			[HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "ServerStatus")] HttpRequest req,
			ILogger log)
		{
			// Validate Configuration
			var (isConfigValid, errors) = SasConfiguration.Validate();

			errors.Select(d => $"{d.Key}: {d.Value}");
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
}
