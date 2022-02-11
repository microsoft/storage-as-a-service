using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas
{
	public static class Roles
	{
		[FunctionName("Roles")]
		public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "Roles")]
			HttpRequest req, ILogger log)
		{
			RolesResult rr = new();

			// Request body is supposed to contain the user's identity claim
			if (req.Body.Length > 0)
			{
				IdentityToken it = await JsonSerializer.DeserializeAsync<IdentityToken>(req.Body);

				log.LogInformation($"Looking for custom roles to assign to '{it.UserDetails}' (number of claims: {it.Claims.Length}).");

				// TODO: Change "." to "-" in custom roles because staticwebapp.config.json doesn't support periods.
				string[] additionalRoles = it.Claims
					// Find any roles claims in the token
					.Where(c => c.Type.Equals("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
							 || c.Type.Equals("roles"))
					// Get those values as an array of strings
					.Select(c => c.Value.Replace(".", ""))
					.ToArray();

				log.LogInformation($"Assigning {additionalRoles.Length} additional role(s) '{string.Join(',', additionalRoles)}' to '{it.UserDetails}'.");

				rr.Roles = additionalRoles;
			}

			return new OkObjectResult(rr);
		}

		private class Claim
		{
			[JsonPropertyName("typ")]
			public string Type { get; set; }

			[JsonPropertyName("val")]
			public string Value { get; set; }
		}

		private class IdentityToken
		{
			[JsonPropertyName("identityProvider")]
			public string IdentityProvider { get; set; }

			[JsonPropertyName("userId")]
			public string UserId { get; set; }

			[JsonPropertyName("userDetails")]
			public string UserDetails { get; set; }

			[JsonPropertyName("claims")]
			public Claim[] Claims { get; set; }

			[JsonPropertyName("accessToken")]
			public string AccessToken { get; set; }
		}

		private class RolesResult
		{
			public string[] Roles { get; set; }
		}
	}
}
