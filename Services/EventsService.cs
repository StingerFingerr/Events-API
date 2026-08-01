using Events_API.Consts;
using Events_API.DTOs;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Results;
using Events_API.Models;

namespace Events_API.Services;

public class EventService : IEventService
{
    private List<Event> Events { get; }

    private int NewEventId
    {
        get => field++;
    } = 1;

    public EventService()
    {
        Events =
        [
            new Event()
            {
                Id = NewEventId, Title = "event 11", 
                StartAt = DateTime.Now.AddDays(1),
                EndAt = DateTime.Now.AddDays(2)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 12", 
                StartAt = DateTime.Now.AddDays(5),
                EndAt = DateTime.Now.AddDays(7)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 23", 
                StartAt = DateTime.Now.AddDays(14),
                EndAt = DateTime.Now.AddDays(15)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 24", 
                StartAt = DateTime.Now.AddDays(16),
                EndAt = DateTime.Now.AddDays(17)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 25", 
                StartAt = DateTime.Now.AddDays(18),
                EndAt = DateTime.Now.AddDays(19)
            },
        ];
    }
    
    public EventDto? GetEventByIdAsync(int id)
    {
        var eventFound = Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            return null;
        return eventFound.AsDto();
    }


    public Result<EventDto> CreateEventAsync(CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage)) 
            return Result<EventDto>.Failure(errorMessage);
        if (EventExistsByTitle(eventData.Title))
            return Result<EventDto>.Failure(ErrorsMessages.EventAlreadyExists);
        
        var newEvent = new Event()
        {
            Id = NewEventId,
            Title = eventData.Title,
            StartAt = eventData.StartAt,
            EndAt = eventData.EndAt,
            Description = eventData.Description
        };

        Events.Add(newEvent);

        return Result<EventDto>.Success(newEvent.AsDto());
    }

    public List<EventDto> GetAllEventsAsync()
    {
        return Events.Select(e => e.AsDto()).ToList();
    }

    public Result<EventDto> UpdateEventAsync(int id, EventUpdateDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage)) 
            return Result<EventDto>.Failure(errorMessage);
        var eventFound = Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            return Result<EventDto>.Failure(ErrorsMessages.EventNotFound);
        eventFound.Title = eventData.Title;
        eventFound.StartAt = eventData.StartAt;
        eventFound.EndAt = eventData.EndAt;
        eventFound.Description = eventData.Description;
        return Result<EventDto>.Success(eventFound.AsDto());
    }

    public Result<EventDto> UpdateEventAsync(int id, string newTitle)
    {
        var eventFound = Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            return Result<EventDto>.Failure(ErrorsMessages.EventNotFound);
        eventFound.Title = newTitle;
        return Result<EventDto>.Success(eventFound.AsDto());
    }

    public Result DeleteEventAsync(int id)
    {
        var success = Events.RemoveAll(e => e.Id == id) != 0;
        if (success)
            return Result.Success();
        return Result.Failure(ErrorsMessages.EventNotFound);
    }

    public GetEventsWithFiltersResult GetEventByFilters(GetEventsWithFiltersDto filters)
    {
        var filtered = Events.AsEnumerable();
        
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
        return Events.Any(e =>
            string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateEventDto(CreateEventDto eventData, out string errorMessage)
    {
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