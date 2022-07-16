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

			IList<StorageAccountAndContainers> result = null;

			if (!forceRefresh)
				result = _cache.GetStorageAccountList(principalId);

			// Result is null in case of a forced refresh, cache timeout, or no cached item
			if (result == null)
			{
				// Retrieve the list from the Azure management plane
				RoleOperations ro = new(_log);

				result = ro.GetAccessibleContainersForPrincipal(principalId);

				// Send the new list to the cache
				_cache.SetStorageAccountList(principalId, result);
			}

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
