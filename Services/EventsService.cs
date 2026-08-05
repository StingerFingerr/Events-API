using Events_API.Consts;
using Events_API.DTOs;
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

    public Result<EventDto> CreateEvent(CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage)) 
            return Result<EventDto>.Failure(errorMessage);
        if (EventExistsByTitle(eventData.Title))
            return Result<EventDto>.Failure(ErrorsMessages.EventAlreadyExists);
        
        var newEvent = new Event()
        {
            Id = repository.NewEventId,
            Title = eventData.Title,
            StartAt = eventData.StartAt,
            EndAt = eventData.EndAt,
            Description = eventData.Description
        };

        repository.Events.Add(newEvent);

        return Result<EventDto>.Success(newEvent.AsDto());
    }

    public List<EventDto> GetAllEvents()
    {
        return repository.Events.Select(e => e.AsDto()).ToList();
    }

    public Result<EventDto> UpdateEvent(int id, EventUpdateDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage)) 
            return Result<EventDto>.Failure(errorMessage);
        var eventFound = repository.Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            throw new NotFoundException();
        eventFound.Title = eventData.Title;
        eventFound.StartAt = eventData.StartAt;
        eventFound.EndAt = eventData.EndAt;
        eventFound.Description = eventData.Description;
        return Result<EventDto>.Success(eventFound.AsDto());
    }

    public Result<EventDto> UpdateEvent(int id, string newTitle)
    {
        var eventFound = repository.Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            throw new NotFoundException();
        eventFound.Title = newTitle;
        return Result<EventDto>.Success(eventFound.AsDto());
    }

    public Result DeleteEvent(int id)
    {
        var eventToDelete = repository.Events.FirstOrDefault(e => e.Id == id);
        if (eventToDelete is null)
            throw new NotFoundException();
        repository.Events.Remove(eventToDelete);
        return Result.Success();
    }

    public GetEventsWithFiltersResult GetEventsByFilters(GetEventsByFiltersDto filters)
    {
        if (filters.Page < 1 || filters.PageSize < 1)
            throw new ArgumentException("Pagination parameters must be greater than or equal to 1.");
        
        var filtered = repository.Events.AsEnumerable();
        
        if(filters.Title is not null)
            filtered = filtered.Where(e => e.Title.Contains(filters.Title, StringComparison.OrdinalIgnoreCase));
        if(filters.From is not null)
            filtered = filtered.Where(e =>  e.StartAt >= filters.From);
        if(filters.To is not null)
            filtered = filtered.Where(e =>  e.StartAt <= filters.To);

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