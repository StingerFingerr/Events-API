using System.Collections.Concurrent;
using Events_API.Models;

namespace Events_API.Services.Events;

public class InMemoryEventsRepository : IEventsRepository
{
    public ConcurrentDictionary<Guid, Event> Events { get; }
    public Guid NewEventId => Guid.NewGuid();

    public InMemoryEventsRepository(ConcurrentDictionary<Guid, Event>? events = null)
    {
        if (events is not null)
        {
            Events = events;
            return;
        }

        var initialEvents = new[]
        {
            new Event(Guid.NewGuid(), "rock festival", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100),
            new Event(Guid.NewGuid(), "rap concert", DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(7), 100),
            new Event(Guid.NewGuid(), "food festival", DateTime.UtcNow.AddDays(14), DateTime.UtcNow.AddDays(15), 100)
        };
        Events = new ConcurrentDictionary<Guid, Event>(initialEvents.ToDictionary(eventData => eventData.Id));
    }
}
