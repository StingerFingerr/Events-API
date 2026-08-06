using System.Collections.Concurrent;
using Events_API.Models;

namespace Events_API.Services;

public class InMemoryEventsRepository : IEventsRepository
{
    public ConcurrentDictionary<int, Event> Events { get; }
    public int NewEventId
    {
        get
        {
            field++;
            LastEventId = field;
            return field;
        }
        private init
        {
            field = value;
            LastEventId = field;
        }
    } = 0;

    private int LastEventId { get; set; }

    public InMemoryEventsRepository(ConcurrentDictionary<int, Event>? events = null)
    {
        if (events is not null)
        {
            Events = events;
            NewEventId = events.Count;
            return;
        }
        
        Events = new ConcurrentDictionary<int, Event>()
        {
            [NewEventId] = new Event(LastEventId, "rock festival",DateTime.Now.AddDays(1),DateTime.Now.AddDays(2)),
            [NewEventId] = new Event(LastEventId, "rap concert",DateTime.Now.AddDays(5),DateTime.Now.AddDays(7)),
            [NewEventId] = new Event(LastEventId, "food festival", DateTime.Now.AddDays(14),  DateTime.Now.AddDays(15))
        };
    }
}