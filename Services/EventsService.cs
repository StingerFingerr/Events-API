using System.Net;
using Events_API.DTOs.Events;
using Events_API.DTOs.Results;
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
                Id = NewEventId, Title = "event 1", StartAt = DateTime.Now + new TimeSpan(7, 0, 0),
                EndAt = DateTime.Now + new TimeSpan(14, 0, 0)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 2", StartAt = DateTime.Now + new TimeSpan(4, 0, 0),
                EndAt = DateTime.Now + new TimeSpan(10, 0, 0)
            },
            new Event()
            {
                Id = NewEventId, Title = "event 3", StartAt = DateTime.Now + new TimeSpan(15, 0, 0),
                EndAt = DateTime.Now + new TimeSpan(20, 0, 0)
            },
        ];
    }


    public async Task<ApiBaseResult> CreateEventAsync(CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var eventAsync)) 
            return eventAsync;
        if(EventExistsByTitle(eventData.Title))
            return ApiResult.Failed("event with the same name already exists.");
        
        var newEvent = new Event()
        {
            Id = NewEventId,
            Title = eventData.Title,
            StartAt = eventData.StartAt,
            EndAt = eventData.EndAt,
        };

        Events.Add(newEvent);

        return new ApiResult<EventDto>()
        {
            Success = true,
            Message = string.Empty,
            StatusCode = HttpStatusCode.Created,
            Data = newEvent.AsDto()
        };
    }

    public async Task<ApiBaseResult> GetEventByIdAsync(int id)
    {
        var eventFound = Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            return ApiResult.Failed("event not found");
        return ApiResult<EventDto>.Created(eventFound.AsDto());
    }

    public async Task<ApiBaseResult> GetAllEventsAsync()
    {
        return ApiResult<List<EventDto>>.Ok(Events
            .Select(e => e.AsDto()) 
            .ToList());
    }

    public async Task<ApiBaseResult> UpdateEventAsync(EventUpdateDto eventData)
    {
        if (ValidateEventDto(eventData, out var result)) 
            return result;
        var eventFound = Events.FirstOrDefault(e => e.Id == eventData.Id);
        if (eventFound is null)
            return ApiResult.Failed("event not found");
        eventFound.Title = eventData.Title;
        eventFound.StartAt = eventData.StartAt;
        eventFound.EndAt = eventData.EndAt;
        return ApiResult<EventDto>.Ok(eventFound.AsDto());
    }

    public async Task<ApiBaseResult> UpdateEventAsync(int id, string newTitle)
    {
        var eventFound = Events.FirstOrDefault(e => e.Id == id);
        if (eventFound is null)
            return ApiResult.Failed("event not found");
        eventFound.Title = newTitle;
        return ApiResult.Ok();
    }

    public async Task<ApiBaseResult> DeleteEventAsync(int id)
    {
        var success = Events.RemoveAll(e => e.Id == id) != 0;
        if (success)
            return ApiResult.Ok();
        return ApiResult.Failed("event not found");
    }

    private bool EventExistsByTitle(string title)
    {
        return Events.Any(e =>
            string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateEventDto(CreateEventDto eventData, out ApiBaseResult result)
    {
        if (eventData.StartAt < DateTime.Now)
        {
            result = ApiResult.Failed("you can't create an event in the past.");
            return true;
        }

        if (eventData.StartAt >= eventData.EndAt)
        {
            result = ApiResult.Failed("you can't create an event with a start date that ends after.");
            return true;
        }

        result = ApiResult.Ok();
        return false;
    }
}