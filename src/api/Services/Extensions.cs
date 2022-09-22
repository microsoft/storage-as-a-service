// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;

namespace Microsoft.UsEduCsu.Saas.Services;

internal static class Extensions
{
	public static bool AnyNull(params object[] args)
	{
		return args.Any(x => x == null);
	}

	public static bool AnyNullOrEmpty(params object[] args)
	{
		return args.Any(x => x == null || string.IsNullOrEmpty(x.ToString()));
	}
}
