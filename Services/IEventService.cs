using Events_API.DTOs;
using Events_API.DTOs.Events;

namespace Events_API.Services;

public interface IEventService
{
    Task<EventDto?> GetEventByIdAsync(int id);
    Task<List<EventDto>> GetAllEventsAsync();
    Task<Result<EventDto>> CreateEventAsync(CreateEventDto eventData);
    Task<Result<EventDto>> UpdateEventAsync(int id, EventUpdateDto eventData);
    Task<Result<EventDto>> UpdateEventAsync(int id, string newTitle);
    Task<Result> DeleteEventAsync(int id); 
}
