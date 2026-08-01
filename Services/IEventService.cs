using Events_API.DTOs;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Results;

namespace Events_API.Services;

public interface IEventService
{
    EventDto? GetEventByIdAsync(int id);
    List<EventDto> GetAllEventsAsync();
    Result<EventDto> CreateEventAsync(CreateEventDto eventData);
    Result<EventDto> UpdateEventAsync(int id, EventUpdateDto eventData);
    Result<EventDto> UpdateEventAsync(int id, string newTitle);
    Result DeleteEventAsync(int id);
    GetEventsWithFiltersResult GetEventByFilters(GetEventsWithFiltersDto filters);
}
