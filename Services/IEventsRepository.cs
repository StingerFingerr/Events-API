using Events_API.Models;

namespace Events_API.Services;

public interface IEventsRepository
{
    public List<Event> Events { get; }
    public int NewEventId { get; }
}