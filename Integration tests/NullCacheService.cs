using Events.Application.Caching;

namespace Integration_tests;

internal sealed class NullCacheService : ICacheService
{
    public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
    public Task SetAsync(string key, string value, TimeSpan timeToLive) => Task.CompletedTask;
    public Task RemoveAsync(string key) => Task.CompletedTask;
}
