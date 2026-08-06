using System.Collections.Concurrent;
using Events_API.Models;

namespace Events_API.Services;

public interface IEventsRepository
{
    public ConcurrentDictionary<int, Event> Events { get; }
    public int NewEventId { get; }
}