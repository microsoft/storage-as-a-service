// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.UsEduCsu.Saas.Services
{
	internal class StorageAccountOperations
	{
		ILogger _log;
		CacheHelper _cache;

		internal StorageAccountOperations(ILogger log)
		{
			_log = log;
			_cache = CacheHelper.GetRedisCacheHelper(_log);
		}

		internal IEnumerable<string> GetAccessibleStorageAccounts(string principalId, bool forceRefresh = false)
		{
			var result = GetFromCache(principalId);
			return result.Select(r => r.StorageAccountName);
		}

		internal IEnumerable<string> GetAccessibleContainerDetails(string principalId,
			string storageAccountName, bool forceRefresh = false)
		{
			var result = GetFromCache(principalId);
			var strAcct = result.FirstOrDefault(r => r.StorageAccountName == storageAccountName);
			return strAcct?.Containers;
		}

		private IList<StorageAccountAndContainers> GetFromCache(string principalId)
		{
			IList<StorageAccountAndContainers> result = _cache.StorageAccounts(principalId, () => {
				RoleOperations ro = new(_log);
				result = ro.GetAccessibleContainersForPrincipal(principalId);
				return result;
			});

			return result;
		}
	}
}
