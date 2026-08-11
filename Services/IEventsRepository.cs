using System.Collections.Concurrent;
using Events_API.Models;

namespace Events_API.Services;

public interface IEventsRepository
{
    public ConcurrentDictionary<Guid, Event> Events { get; }
    public Guid NewEventId { get; }
}
