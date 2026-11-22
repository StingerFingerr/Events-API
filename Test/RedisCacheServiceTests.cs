using Events.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Test;

public class RedisCacheServiceTests
{
    private readonly Mock<IDatabase> database = new(MockBehavior.Strict);
    private readonly Mock<ILogger<RedisCacheService>> logger = new();

    private RedisCacheService CreateCache()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        connection.Setup(x => x.GetDatabase(-1, null)).Returns(database.Object);
        return new RedisCacheService(() => connection.Object, logger.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cached value")]
    public async Task Get_ReturnsValueOrCacheMiss(string? value)
    {
        database.Setup(x => x.StringGetAsync("event:1", CommandFlags.None))
            .ReturnsAsync((RedisValue)value);

        Assert.Equal(value, await CreateCache().GetAsync("event:1"));
    }

    [Fact]
    public async Task ReadFailure_ReturnsCacheMissAndLogsWarning()
    {
        database.Setup(x => x.StringGetAsync("event:1", CommandFlags.None))
            .ThrowsAsync(new RedisTimeoutException(CommandFlags.None, "Redis unavailable", CommandStatus.Unknown));

        Assert.Null(await CreateCache().GetAsync("event:1"));
        VerifyWarnings(1);
    }

    [Fact]
    public async Task InitialConnectionFailure_DoesNotEscapeAnyOperation()
    {
        var cache = new RedisCacheService(
            () => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "Offline", null, CommandStatus.Unknown),
            logger.Object);

        Assert.Null(await cache.GetAsync("event:1"));
        await cache.SetAsync("event:1", "value", TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("event:1");
        VerifyWarnings(3);
    }

    [Fact]
    public async Task RemoveFailure_IsLoggedAndSuppressed()
    {
        database.Setup(x => x.KeyDeleteAsync("event:1", CommandFlags.None))
            .ThrowsAsync(new RedisTimeoutException(CommandFlags.None, "Redis unavailable", CommandStatus.Unknown));

        await CreateCache().RemoveAsync("event:1");
        VerifyWarnings(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Set_UsesRequestedTtlAndSuppressesRedisFailure(bool unavailable)
    {
        var ttl = TimeSpan.FromMinutes(5);
        var setup = database.Setup(x => x.StringSetAsync(
            "event:1", "value", ttl, false, When.Always, CommandFlags.None));
        if (unavailable)
            setup.ThrowsAsync(new RedisTimeoutException(CommandFlags.None, "Redis unavailable", CommandStatus.Unknown));
        else
            setup.ReturnsAsync(true);

        await CreateCache().SetAsync("event:1", "value", ttl);

        database.Verify(x => x.StringSetAsync(
            "event:1", "value", ttl, false, When.Always, CommandFlags.None), Times.Once);
        VerifyWarnings(unavailable ? 1 : 0);
    }

    private void VerifyWarnings(int count) => logger.Verify(x => x.Log(
        LogLevel.Warning,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(count));
}
