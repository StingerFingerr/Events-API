using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;

namespace Events_API.Services.Events;

public interface IEventService
{
    EventDto GetEventById(Guid id);
    EventDto CreateEvent(CreateEventDto eventData);
    EventDto UpdateEvent(Guid id, CreateEventDto eventData);
    EventDto UpdateEvent(Guid id, string newTitle);
    void DeleteEvent(Guid id);
    PaginatedResult<EventDto> GetEventsByFilters(GetEventsByFiltersDto filters);
}
