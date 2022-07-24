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
			var obj = Items("directoryObject", objectIdentifier, updateMethod);
			return obj;
		}

		public IList<string> AccessibleContainers(string objectIdentifier, Func<IList<string>> updateMethod)
		{
			var obj = Items("accessibleContainers", objectIdentifier, updateMethod, DateTimeOffset.Now.AddMinutes(5));
			return obj;
		}

		internal  IList<StorageAccountAndContainers> StorageAccounts(string principalId, Func<IList<StorageAccountAndContainers>> updateMethod)
		{
			var obj = Items("storageAccountList", principalId, updateMethod);
			return obj;
		}

		public string AccessTokens(string objectIdentifier, Func<string> updateMethod)
		{
			var obj = Items("accessToken", objectIdentifier, updateMethod);
			return obj;
		}

		#region AccessToken
		public string GetAccessToken(string principalId)
		{
			string nameKey = $"accessToken_{principalId}";
			var data = _cache.Get(nameKey);
			if (data == null)
				return string.Empty;
			return JsonSerializer.Deserialize<string>(data);
		}

		public void SetAccessToken(string principalId, string value)
		{
			MemoryStream s = new();
			JsonSerializer.Serialize(s, value, typeof(string));
			s.Flush();
			var expiration = DateTimeOffset.UtcNow.AddHours(1);
			string nameKey = $"accessToken_{principalId}";
			var data = s.ToArray();
			_cache.Set(nameKey, data, new() { AbsoluteExpiration = expiration });
		}
		#endregion

		public static CacheHelper GetRedisCacheHelper(ILogger log)
		{
			var cacheConnection = Environment.GetEnvironmentVariable("CacheConnection");
			IDistributedCache cache = new RedisCache(new RedisCacheOptions() { Configuration = cacheConnection });
			var ch = new CacheHelper(log, cache);
			return ch;
		}

		#region Private Methods
		private T Items<T>(string itemName, string key, Func<T> updateMethod, DateTimeOffset? expiration = null)
		{
			ArgumentNullException.ThrowIfNull(itemName, nameof(itemName));
			ArgumentNullException.ThrowIfNull(key, nameof(key));

			if (expiration == null)
				expiration = DateTimeOffset.UtcNow.AddHours(1);

			string nameKey = $"{itemName}_{key}";

			byte[] byteArray = _cache.Get(nameKey);

			// The cache will return value if f
			if (byteArray != null)
				return JsonSerializer.Deserialize<T>(byteArray);

			// Get User by invoking Function
			T value = updateMethod.Invoke();
			if (value == null)
				return default;

			// Serialize the object to a UTF-8 JSON string
			MemoryStream s = new();
			JsonSerializer.Serialize(s, value, typeof(T));
			s.Flush();
			var data = s.ToArray();

			// Add the list of accounts to the cache for the specified user, item will expire one hour from now
			_cache.Set(nameKey, data, new() { AbsoluteExpiration = expiration });

			return value;
		}
		#endregion
	}
}