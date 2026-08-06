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
        if (repository.Events.TryGetValue(id, out var eventData))
            return eventData.AsDto();
        throw new NotFoundException();
    }

    public EventDto CreateEvent(CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);

        if(EventExistsByTitle(eventData.Title))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var newEvent = new Event(repository.NewEventId, eventData.Title, eventData.Description, eventData.StartAt,
            eventData.EndAt);
        
        if (repository.Events.TryAdd(newEvent.Id, newEvent))
            return newEvent.AsDto();
        
        throw new ConflictException(ErrorsMessages.InternalServerError);
    }

    public EventDto UpdateEvent(int id, CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);

        if (repository.Events.TryGetValue(id, out var eventFound))
        {
            eventFound.Title = eventData.Title;
            eventFound.StartAt = eventData.StartAt;
            eventFound.EndAt = eventData.EndAt;
            eventFound.Description = eventData.Description;
            return eventFound.AsDto();
        }

        throw new NotFoundException();
    }

    public EventDto UpdateEvent(int id, string newTitle)
    {
        if(EventExistsByTitle(newTitle))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);
        
        if (repository.Events.TryGetValue(id, out var eventFound))
        {
            eventFound.Title = newTitle;
            return eventFound.AsDto();
        }

        throw new NotFoundException();
    }

    public void DeleteEvent(int id)
    {
        if(repository.Events.Remove(id, out _) is false)
            throw new NotFoundException();
    }

    public PaginatedResult<EventDto> GetEventsByFilters(GetEventsByFiltersDto filters)
    {
        if (filters.Page < 1 || filters.PageSize < 1)
            throw new ValidationException("Pagination parameters must be greater than or equal to 1.");

        var filtered = repository.Events.AsEnumerable();

        if (filters.Title is not null)
            filtered = filtered.Where(e => e.Value.Title.Contains(filters.Title, StringComparison.OrdinalIgnoreCase));
        if (filters.From is not null)
            filtered = filtered.Where(e => e.Value.StartAt >= filters.From);
        if (filters.To is not null)
            filtered = filtered.Where(e => e.Value.StartAt <= filters.To);

        var items = filtered
            .OrderBy(e => e.Value.StartAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(e => e.Value.AsDto())
            .ToList();

        var totalItems = filtered.Count();
        var totalPages = (int)Math.Ceiling((double)totalItems / filters.PageSize);

        return new PaginatedResult<EventDto>(items, filters.Page, filters.PageSize, totalItems, totalPages);
    }

    private bool EventExistsByTitle(string title) => 
        repository.Events.Any(e => e.Value.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

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
            errorMessage = ErrorsMessages.CannotCreateEventWithStartLaterThenEnd;
            return true;
        }

        errorMessage = string.Empty;
        return false;
    }
}