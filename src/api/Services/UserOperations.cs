using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.UsEduCsu.Saas.Services
{
	public class UserOperations
	{
		private ILogger log;
		private TokenCredential tokenCredential;

		public UserOperations(ILogger log, TokenCredential tokenCredential)
		{
			this.log = log;
			this.tokenCredential = tokenCredential;
		}

		public async Task<string> GetObjectIdFromUPN(string upn)
		{
			// TODO: Consider moving this method to GroupOperations and renaming GroupOperations to GraphClientOperations
			try
			{
				var tokenRequestContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
				var cancellationToken = new CancellationToken();
				var accessToken = await tokenCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
				var authProvider = new DelegateAuthenticationProvider((requestMessage) =>
				{
					requestMessage
						.Headers
						.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
					requestMessage
						.Headers
						.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

					return Task.FromResult(0);
				});

				var graphClient = new GraphServiceClient(authProvider);

				// Retrieve a user by userPrincipalName
				var user = await graphClient
					.Users[upn]
					.Request()
					.GetAsync(cancellationToken);

				return user?.Id;    // TODO: Opportunity for caching
			}
			catch (Exception ex)
			{
				log.LogError(ex, ex.Message);
				return null;
			}
		}

		public static ClaimsPrincipal GetClaimsPrincipal(HttpRequest req)
		{
			var principal = new ClientPrincipal();

			if (req.Headers.TryGetValue("x-ms-client-principal", out var header))
			{
				var data = header[0];
				var decoded = Convert.FromBase64String(data);
				var json = Encoding.UTF8.GetString(decoded);
				principal = JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			}

			// TODO: Document why the 'anonymous' role is being excluded?
			principal.UserRoles = principal.UserRoles?.Except(new string[] { "anonymous" }, StringComparer.CurrentCultureIgnoreCase);

			// If there are no roles left after removing the 'anonymous' role
			if (!principal.UserRoles?.Any() ?? true)
			{
				// Return a default ClaimsPrincipal
				return new ClaimsPrincipal();
			}

			// There are role(s) other than 'anonymous' in the claim
			var identity = new ClaimsIdentity(principal.IdentityProvider);
			identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principal.UserId));
			identity.AddClaim(new Claim(ClaimTypes.Name, principal.UserDetails));
			identity.AddClaims(principal.UserRoles.Select(r => new Claim(ClaimTypes.Role, r)));

			// Add default claims
			if (principal.Claims == null)
			{
				principal.Claims = new List<ClientPrincipal.Claim>()
				{
					new ClientPrincipal.Claim("iss", $"https://login.microsoftonline.com/{SasConfiguration.TenantId}/v2.0"),
					new ClientPrincipal.Claim("aud", SasConfiguration.ClientId),
					new ClientPrincipal.Claim("http://schemas.microsoft.com/identity/claims/tenantid",SasConfiguration.TenantId),
					new ClientPrincipal.Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", principal.UserId)
				};

			}

			// Join all the existing claims
			foreach (var principalClaim in principal.Claims)
			{
				identity.AddClaim(new Claim(principalClaim.Typ, principalClaim.Val));
			}

			return new ClaimsPrincipal(identity);
		}

		internal static string GetUserPrincipalId(ClaimsPrincipal claimsPrincipal)
		{
			return claimsPrincipal.Claims.FirstOrDefault(fa => fa.Type == ClaimTypes.NameIdentifier)?.Value;
		}

		private class ClientPrincipal
		{
			public string IdentityProvider { get; set; }
			public string UserId { get; set; }
			public string UserDetails { get; set; }
			public IEnumerable<string> UserRoles { get; set; }
			public IEnumerable<ClientPrincipal.Claim> Claims { get; set; }

			public class Claim
			{
				public Claim(string typ, string val)
				{
					Typ = typ;
					Val = val;
				}
				public string Typ { get; set; }
				public string Val { get; set; }
			}
		}
	}
}