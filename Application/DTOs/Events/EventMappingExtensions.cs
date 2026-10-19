using Events_API.DTOs.Events.Results;

namespace Events_API.Models;

public static class EventMappingExtensions
{
    public static EventDto AsDto(this Event eventData) =>
        new(eventData.Id, eventData.Title, eventData.Description, eventData.StartAt,
            eventData.EndAt, eventData.TotalSeats, eventData.AvailableSeats);
}
