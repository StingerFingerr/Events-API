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
            new Event(Guid.NewGuid(), "rock festival", DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)),
            new Event(Guid.NewGuid(), "rap concert", DateTime.Now.AddDays(5), DateTime.Now.AddDays(7)),
            new Event(Guid.NewGuid(), "food festival", DateTime.Now.AddDays(14), DateTime.Now.AddDays(15))
        };
        Events = new ConcurrentDictionary<Guid, Event>(initialEvents.ToDictionary(eventData => eventData.Id));
    }
}
