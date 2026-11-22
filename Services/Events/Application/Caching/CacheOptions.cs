namespace Events.Application.Caching;

public sealed class CacheOptions
{
    public int EventTtlSeconds { get; set; } = 300;
    public int Top10TtlSeconds { get; set; } = 300;

    public void Validate()
    {
        if (EventTtlSeconds <= 0 || Top10TtlSeconds <= 0)
            throw new InvalidOperationException("Cache TTL values must be greater than zero seconds.");
    }
}
