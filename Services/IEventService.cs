using Events_API.DTOs;
using Events_API.DTOs.Events;

namespace Events_API.Services;

public interface IEventService
{
    EventDto? GetEventByIdAsync(int id);
    List<EventDto> GetAllEventsAsync();
    Result<EventDto> CreateEventAsync(CreateEventDto eventData);
    Result<EventDto> UpdateEventAsync(int id, EventUpdateDto eventData);
    Result<EventDto> UpdateEventAsync(int id, string newTitle);
    Result DeleteEventAsync(int id); 
}
