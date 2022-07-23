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

			IList<StorageAccountAndContainers> result = _cache.StorageAccounts(principalId, () => {
				RoleOperations ro = new(_log);
				result = ro.GetAccessibleContainersForPrincipal(principalId);
				return result;
			});

			return result.Select(r => r.StorageAccountName);
		}

		internal IList<StorageAccountAndContainers> GetAccessibleContainerDetails(string principalId,
			string storageAccountName, bool forceRefresh = false)
		{
			throw new NotImplementedException();

			// Pull data from cache
			// Refresh if needed GetAccessibleStorageAccounts
			// Filter on storage accounts, pull the containers

		}
	}
}
