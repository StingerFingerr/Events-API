using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;

namespace Events_API.Services.Events;

public interface IEventService
{
    Task<EventDto> GetEventById(Guid id);
    Task<EventDto> CreateEvent(CreateEventDto eventData);
    Task<EventDto> UpdateEvent(Guid id, CreateEventDto eventData);
    Task<EventDto> UpdateEvent(Guid id, string newTitle);
    Task DeleteEvent(Guid id);
    Task<PaginatedResult<EventDto>> GetEventsByFilters(GetEventsByFiltersDto filters);
}
