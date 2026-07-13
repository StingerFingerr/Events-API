using Events_API.DTOs.Events;
using Events_API.DTOs.Results;

namespace Events_API.Services;

public interface IEventService
{
    Task<ApiBaseResult> CreateEventAsync(CreateEventDto eventData);
    Task<ApiBaseResult> GetEventByIdAsync(int id);
    Task<ApiBaseResult> GetAllEventsAsync();
    Task<ApiBaseResult> UpdateEventAsync(EventUpdateDto eventData);
    Task<ApiBaseResult> UpdateEventAsync(int id, string newTitle);
    Task<ApiBaseResult> DeleteEventAsync(int id);
}
