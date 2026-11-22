namespace Events.Application.Caching;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan timeToLive);
    Task RemoveAsync(string key);
}
