using Events.Domain.Models;
using Events.Application.DTOs.Events.Results;

namespace Events.Application.DTOs.Events;

public static class EventMappingExtensions
{
    public static EventDto AsDto(this Event eventData) =>
        new(eventData.Id, eventData.Title, eventData.Description, eventData.StartAt,
            eventData.EndAt, eventData.TotalSeats, eventData.AvailableSeats);
}
