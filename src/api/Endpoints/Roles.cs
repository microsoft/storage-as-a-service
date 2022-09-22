// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas;

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

			log.LogInformation("Looking for custom roles to assign to '{userDetails}' (number of claims: {claimsLength}).",
				it.UserDetails, it.Claims.Length);

			// TODO: Change "." to "-" in custom roles because staticwebapp.config.json doesn't support periods.
			string[] additionalRoles = it.Claims
				// Find any roles claims in the token
				.Where(c => c.Type.Equals("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", System.StringComparison.OrdinalIgnoreCase)
						 || c.Type.Equals("roles", System.StringComparison.OrdinalIgnoreCase))
				// Get those values as an array of strings
				.Select(c => c.Value.Replace(".", ""))
				.ToArray();

			log.LogInformation("Assigning {additionalRolesLength} additional role(s) '{additionalRoles}' to '{userDetails}'.",
				additionalRoles.Length, string.Join(',', additionalRoles), it.UserDetails);

			rr.Roles = additionalRoles;

			// Cache the accessToken
			if (it.AccessToken != null)
			{
				CacheHelper.GetRedisCacheHelper(log).SetAccessToken(it.UserId, it.AccessToken);
			}
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
