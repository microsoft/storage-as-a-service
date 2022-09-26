// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Rest;
using Microsoft.UsEduCsu.Saas.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Microsoft.UsEduCsu.Saas.Services;

internal sealed class CacheHelper
{
	private readonly IDistributedCache _cache;
	private readonly ILogger _logger;

	public CacheHelper(ILogger log, IDistributedCache cache)
	{
		_cache = cache;
		_logger = log;
	}

	#region Public Accessors
	/// <summary>
	/// Returns a cached directory objects
	/// </summary>
	/// <param name="objectIdentifier">A unique identifier to lookup in the cache</param>
	/// <param name="updateMethod">Parameterless Func method that returns a DirectoryObject</param>
	/// <returns>Cached value or new value if not cached</returns>
	public DirectoryObject DirectoryObjects(string objectIdentifier, Func<DirectoryObject> updateMethod)
	{
		var obj = Items("directoryObject", objectIdentifier, updateMethod);
		return obj;
	}

	/// <summary>
	/// Returns the cached list of storage accounts and containers the
	/// specified principal has access to.
	/// </summary>
	/// <param name="objectIdentifier">A user principal ID to lookup in the cache.</param>
	/// <param name="updateMethod">Parameterless Func method that returns a IList<StorageAccountAndContainers> object.</param>
	/// <returns>Cached value or new value if not cached.</returns>
	internal IList<StorageAccountAndContainers> StorageAccounts(string principalId, Func<IList<StorageAccountAndContainers>> updateMethod)
	{
		var obj = Items("storageAccountList", principalId, updateMethod);
		return obj;
	}

	/// <summary>
	/// Returns the cached list of storage account properties
	/// </summary>
	/// <returns>Cached value or new value if not cached.</returns>
	internal StorageAccountProperties GetStorageAccountProperties()
	{
		if (String.IsNullOrWhiteSpace(Configuration.StorageAccountPropertiesCacheKey)) { return null; }
		var obj = GetCacheValue<StorageAccountProperties>(Configuration.StorageAccountPropertiesCacheKey);
		if (obj is null) { obj = new StorageAccountProperties(); }
		return obj;

	}

	/// <summary>
	/// Sets the cached list of storage account properties
	/// </summary>
	/// <returns>Cached value or new value if not cached.</returns>
	internal void SetStorageAccountProperties(StorageAccountProperties properties)
	{
		try
		{
			SetCacheValue(Configuration.StorageAccountPropertiesCacheKey, properties);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error caching storageaccount properties");
		}
	}


	/// <summary>
	/// Returns a cached directory objects
	/// </summary>
	/// <param name="principalId">A unique identifier to lookup in the cache</param>
	/// <param name="updateMethod">Parameterless Func method that returns a DirectoryObject</param>
	/// <returns>Cached value or new value if not cached</returns>
	public string AccessTokens(string principalId, Func<string> updateMethod)
	{
		var obj = Items("accessToken", principalId, updateMethod);
		return obj;
	}
	#endregion

	#region AccessToken Accessors
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
	/// <summary>
	///		Generic method to store items in the cache and repopulate based on a a function method provided
	/// </summary>
	/// <typeparam name="T">Any JSON serializable object.</typeparam>
	/// <param name="itemName">item name or category to separate items</param>
	/// <param name="key">A unique identifier to lookup in the cache</param>
	/// <param name="updateMethod">Parameterless Func method that returns the object to add to the cache.</param>
	/// <param name="expiration">Optional DateTimeOffset to expire cache. If not specified, the default is 1 hour.</param>
	/// <returns>Cached value or new value if not cached.</returns>
	private T Items<T>(string itemName, string key, Func<T> updateMethod, DateTimeOffset? expiration = null)
	{
		// Verify Arguments
		ArgumentNullException.ThrowIfNull(itemName, nameof(itemName));
		ArgumentNullException.ThrowIfNull(key, nameof(key));

		if (expiration == null)
			expiration = DateTimeOffset.UtcNow.AddHours(1);

		// Build the Key for the cache
		string nameKey = $"{itemName}_{key}";

		// Get Value from cache if found
		// TODO: Handle RedisTimeoutException (retry)
		byte[] byteArray = _cache.Get(nameKey);

		// The cache will return value if found
		// TODO: Consider ignoring cached value if 2 bytes only (empty JSON object)
		if (byteArray != null)
		{
			// convert byte array to string
			var obj = JsonSerializer.Deserialize<T>(byteArray);
			_logger.LogDebug($"{nameKey} (bytes: {byteArray.Length}) pulled from cache.");
			return obj;
		}

		// Get User by invoking Function
		T value = updateMethod.Invoke();
		if (value == null)
			return default;


		SetCacheValue(nameKey, value, expiration);

#if DEBUG
		// Serialization DoubleCheck
		var cacheValue = GetCacheValue<T>(nameKey);
		if (value is IEquatable<T> && !value.Equals(cacheValue))
			throw new SerializationException("Unable to serialize object.");
#endif

		return value;
	}

	private T GetCacheValue<T>(string nameKey)
	{
		// Get Value from cache if found
		byte[] byteArray = _cache.Get(nameKey);

		// The cache will return value if found
		if (byteArray != null)
		{
			var obj = JsonSerializer.Deserialize<T>(byteArray);
			return obj;
		}
		return default;
	}

	private void SetCacheValue<T>(string nameKey, T value, DateTimeOffset? expiration = null)
	{
		MemoryStream s = new();
		JsonSerializer.Serialize(s, value, typeof(T));
		s.Flush();
		var data = s.ToArray();

		// Add the list of accounts to the cache for the specified user, item will expire one hour from now
		_cache.Set(nameKey, data, new() { AbsoluteExpiration = expiration });
		_logger.LogDebug($"{nameKey} (bytes: {data.Length}) written to cache.");
	}

	#endregion
}
