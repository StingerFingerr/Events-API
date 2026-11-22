using System.Net.Sockets;
using Events.Application.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Events.Infrastructure.Caching;

public sealed class RedisCacheService(
    Func<IConnectionMultiplexer> getConnection,
    ILogger<RedisCacheService> logger) : ICacheService
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await getConnection().GetDatabase().StringGetAsync(key);
            return value.IsNull ? null : value.ToString();
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException or SocketException)
        {
            logger.LogWarning(exception, "Redis read failed; treating the request as a cache miss.");
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan timeToLive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);

        try
        {
            await getConnection().GetDatabase().StringSetAsync(
                key, value, timeToLive, keepTtl: false, when: When.Always, flags: CommandFlags.None);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException or SocketException)
        {
            logger.LogWarning(exception, "Redis write failed; skipping cache update.");
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await getConnection().GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException or SocketException)
        {
            logger.LogWarning(exception, "Redis delete failed; skipping cache invalidation.");
        }
    }
}
