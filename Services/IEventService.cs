using Events_API.DTOs;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;

namespace Events_API.Services;

public interface IEventService
{
    EventDto GetEventById(int id);
    EventDto CreateEvent(CreateEventDto eventData);
    EventDto UpdateEvent(int id, CreateEventDto eventData);
    EventDto UpdateEvent(int id, string newTitle);
    void DeleteEvent(int id);
    PaginatedResult<EventDto> GetEventsByFilters(GetEventsByFiltersDto filters);
}
