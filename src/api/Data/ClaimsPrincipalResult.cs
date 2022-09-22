// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Claims;

namespace Microsoft.UsEduCsu.Saas;

internal class ClaimsPrincipalResult
{
	// TODO: Refactor UserOperations to return this instead of a ClaimsPrincipal
	// Move some of this logic into UserOperations.GetClaimsPrincipal

	/// <summary>
	/// Constructs a potentially valid ClaimsPrincipalResult using the specified ClaimsPrincipal.
	/// </summary>
	/// <param name="cp"></param>
	/// <exception cref="ArgumentNullException"></exception>
	public ClaimsPrincipalResult(ClaimsPrincipal cp)
	{
		if (!Services.Extensions.AnyNull(cp, cp.Identity))
		{
			ClaimsPrincipal = cp;
			// IsValid is false by default, only need to set if it's a valid principal
			IsValid = true;
		}
		else
		{
			ClaimsPrincipal = null;
			Message = "Call requires an authenticated user.";
		}
	}

	/// <summary>
	/// Constructs an invalid ClaimsPrincipalResult using the specified error message.
	/// </summary>
	/// <param name="errorMessage"></param>
	/// <exception cref="ArgumentNullException"></exception>
	public ClaimsPrincipalResult(string errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentNullException(nameof(errorMessage));

		ClaimsPrincipal = null;
		Message = errorMessage;
	}

	public bool IsValid { get; set; }
	public string Message { get; set; }
	public ClaimsPrincipal ClaimsPrincipal { get; private set; }
	// TODO: Add UserPrincipalId property as a shortcut
}
