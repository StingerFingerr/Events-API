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
            [NewEventId] = new Event()
            {
                Id = LastEventId, Title = "rock festival",
                StartAt = DateTime.Now.AddDays(1),
                EndAt = DateTime.Now.AddDays(2)
            },
            [NewEventId] = new Event()
            {
                Id = LastEventId, Title = "rap concert",
                StartAt = DateTime.Now.AddDays(5),
                EndAt = DateTime.Now.AddDays(7)
            },
            [NewEventId] = new Event()
            {
                Id = LastEventId, Title = "food festival",
                StartAt = DateTime.Now.AddDays(14),
                EndAt = DateTime.Now.AddDays(15)
            }
        };
    }
}