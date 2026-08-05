using Events_API.DTOs;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;

namespace Events_API.Services;

public interface IEventService
{
    EventDto GetEventById(int id);
    List<EventDto> GetAllEvents();
    EventDto CreateEvent(CreateEventDto eventData);
    EventDto UpdateEvent(int id, EventUpdateDto eventData);
    EventDto UpdateEvent(int id, string newTitle);
    void DeleteEvent(int id);
    GetEventsWithFiltersResult GetEventsByFilters(GetEventsByFiltersDto filters);
}
