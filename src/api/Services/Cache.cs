using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.UsEduCsu.Saas.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.UsEduCsu.Saas.Services
{
	public class CacheHelper
	{
		private readonly IDistributedCache _cache;
		private readonly ILogger _logger;

		public CacheHelper(ILogger log, IDistributedCache cache)
		{
			_cache = cache;
			_logger = log;
		}

		public DirectoryObject DirectoryObjects(string objectIdentifier, Func<DirectoryObject> updateMethod)
		{
			ArgumentNullException.ThrowIfNull(objectIdentifier, nameof(objectIdentifier));

			byte[] list = _cache.Get($"directoryObject_{objectIdentifier}");

			// The cache will return value if f
			if (list != null)
				return JsonSerializer.Deserialize<DirectoryObject>(list);

			// Get User by invoking Function
			DirectoryObject dirObj = updateMethod.Invoke();
			if (dirObj == null)
				return null;

			// Serialize the storageAccountList to a UTF-8 JSON string
			MemoryStream s = new();
			JsonSerializer.Serialize(s, dirObj, typeof(DirectoryObject));

			// Add the list of accounts to the cache for the specified user, item will expire one hour from now
			_cache.Set($"directoryObject_{objectIdentifier}", s.ToArray(), new() { AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1) });

			return dirObj;
		}

		public string GetAccessToken(string userName)
		{
			var data = _cache.Get("AccessToken" + userName);
			if (data == null)
				return string.Empty;
			var result = Encoding.UTF8.GetString(data);
			return result;
		}

		internal IList<StorageAccountAndContainers> GetStorageAccountList(string principalId)
		{
			ArgumentNullException.ThrowIfNull(principalId, nameof(principalId));

			byte[] list = _cache.Get($"storageAccountList_{principalId}");

			// The cache will return null if the item isn't found (including when expired)
			if (list == null) return null;

			return JsonSerializer.Deserialize<List<StorageAccountAndContainers>>(list);
		}

		/// <summary>
		/// Sends the list of storage account and containers for
		/// the specified user to the cache with an expiration of one hour.
		/// </summary>
		/// <param name="principalId"></param>
		/// <param name="storageAccountList"></param>
		internal void SetStorageAccountList(string principalId, IList<StorageAccountAndContainers> storageAccountList)
		{
			ArgumentNullException.ThrowIfNull(principalId, nameof(principalId));
			ArgumentNullException.ThrowIfNull(storageAccountList, nameof(storageAccountList));

			// This item will expire one hour from now
			DistributedCacheEntryOptions dceo = new() { AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1) };

			// Serialize the storageAccountList to a UTF-8 JSON string
			MemoryStream s = new();
			JsonSerializer.Serialize(s, storageAccountList, typeof(IList<StorageAccountAndContainers>));

			// Add the list of accounts to the cache for the specified user
			_cache.Set($"storageAccountList_{principalId}", s.ToArray(), dceo);
		}

		public void SetAccessToken(string userName, string data)
		{
			var bytes = Encoding.UTF8.GetBytes(data);
			_cache.Set("AccessToken" + userName, bytes);
		}

		public static CacheHelper GetRedisCacheHelper(ILogger log)
		{
			var cacheConnection = Environment.GetEnvironmentVariable("CacheConnection");
			IDistributedCache cache = new RedisCache(new RedisCacheOptions() { Configuration = cacheConnection });
			var ch = new CacheHelper(log, cache);
			return ch;
		}
	}
}