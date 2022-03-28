using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using Microsoft.Extensions.Options;

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

        public string GetAccessToken(string userName)
        {
            var data = _cache.Get("AccessToken" + userName);
            if (data == null)
                return string.Empty;
            var result = Encoding.UTF8.GetString(data);
            return result;
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