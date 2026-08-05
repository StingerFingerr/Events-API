using System.ComponentModel.DataAnnotations;
using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;
using Events_API.Exceptions;
using Events_API.Models;

namespace Events_API.Services;

public class EventsService(IEventsRepository repository) : IEventService
{
    public EventDto GetEventById(int id)
    {
        var eventFound = repository.Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            throw new NotFoundException();
        return eventFound.AsDto();
    }

    public EventDto CreateEvent(CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);
        if (EventExistsByTitle(eventData.Title))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var newEvent = new Event()
        {
            Id = repository.NewEventId,
            Title = eventData.Title,
            StartAt = eventData.StartAt,
            EndAt = eventData.EndAt,
            Description = eventData.Description
        };

        repository.Events.Add(newEvent);

        return newEvent.AsDto();
    }

    public List<EventDto> GetAllEvents()
    {
        return repository.Events.Select(e => e.AsDto()).ToList();
    }

    public EventDto UpdateEvent(int id, EventUpdateDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);
        var eventFound = repository.Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            throw new NotFoundException();
        eventFound.Title = eventData.Title;
        eventFound.StartAt = eventData.StartAt;
        eventFound.EndAt = eventData.EndAt;
        eventFound.Description = eventData.Description;
        return eventFound.AsDto();
    }

    public EventDto UpdateEvent(int id, string newTitle)
    {
        if(EventExistsByTitle(newTitle))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);
        
        var eventFound = repository.Events.FirstOrDefault(e => e.Id == id);
        
        if (eventFound is null)
            throw new NotFoundException();
        
        eventFound.Title = newTitle;
        return eventFound.AsDto();
    }

    public void DeleteEvent(int id)
    {
        var eventToDelete = repository.Events.FirstOrDefault(e => e.Id == id);
        if (eventToDelete is null)
            throw new NotFoundException();
        var removed = repository.Events.Remove(eventToDelete);

        if (removed is false)
            throw new NotFoundException();
    }

    public GetEventsWithFiltersResult GetEventsByFilters(GetEventsByFiltersDto filters)
    {
        if (filters.Page < 1 || filters.PageSize < 1)
            throw new ValidationException("Pagination parameters must be greater than or equal to 1.");

        var filtered = repository.Events.AsEnumerable();

        if (filters.Title is not null)
            filtered = filtered.Where(e => e.Title.Contains(filters.Title, StringComparison.OrdinalIgnoreCase));
        if (filters.From is not null)
            filtered = filtered.Where(e => e.StartAt >= filters.From);
        if (filters.To is not null)
            filtered = filtered.Where(e => e.StartAt <= filters.To);

        var items = filtered
            .OrderBy(e => e.StartAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(e => e.AsDto())
            .ToList();

        var totalItems = filtered.Count();
        var totalPages = (int)Math.Ceiling((double)totalItems / filters.PageSize);

        return new GetEventsWithFiltersResult(items, filters.Page, filters.PageSize, totalItems, totalPages);
    }

    private bool EventExistsByTitle(string title)
    {
        return repository.Events.Any(e =>
            string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateEventDto(CreateEventDto eventData, out string errorMessage)
    {
        if (eventData.Title.Length <= 3)
        {
            errorMessage = ErrorsMessages.EventTitleIsShort;
            return true;
        }

        if (eventData.StartAt < DateTime.Now)
        {
            errorMessage = ErrorsMessages.CannotCreateEventInThePast;
            return true;
        }

        if (eventData.StartAt >= eventData.EndAt)
        {
            errorMessage = ErrorsMessages.CannotCreateEventEndsAfterStarts;
            return true;
        }

        errorMessage = string.Empty;
        return false;
    }
}