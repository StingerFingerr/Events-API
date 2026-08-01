using Events_API.Models;

namespace Events_API.Services;

public class InMemoryEventsRepository : IEventsRepository
{
    public List<Event> Events { get; }
    public int NewEventId
    {
        get => field++;
    } = 1;

    public InMemoryEventsRepository(List<Event>? events = null)
    {
        if (events is not null)
        {
            Events = events;
            return;
        }
        Events =
        [
            new Event()
            {
                Id = NewEventId, Title = "event 11", 
                StartAt = DateTime.Now.AddDays(1),
                EndAt = DateTime.Now.AddDays(2)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 12", 
                StartAt = DateTime.Now.AddDays(5),
                EndAt = DateTime.Now.AddDays(7)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 23", 
                StartAt = DateTime.Now.AddDays(14),
                EndAt = DateTime.Now.AddDays(15)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 24", 
                StartAt = DateTime.Now.AddDays(16),
                EndAt = DateTime.Now.AddDays(17)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 25", 
                StartAt = DateTime.Now.AddDays(18),
                EndAt = DateTime.Now.AddDays(19)
            },
        ];
    }
}