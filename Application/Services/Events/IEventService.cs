using Application.DTOs.Events.Incoming;
using Application.DTOs.Events.Results;

namespace Application.Services.Events;

public interface IEventService
{
    Task<EventDto> GetEventById(Guid id);
    Task<EventDto> CreateEvent(CreateEventDto eventData);
    Task<EventDto> UpdateEvent(Guid id, CreateEventDto eventData);
    Task<EventDto> UpdateEvent(Guid id, string newTitle);
    Task DeleteEvent(Guid id);
    Task<PaginatedResult<EventDto>> GetEventsByFilters(GetEventsByFiltersDto filters);
}
