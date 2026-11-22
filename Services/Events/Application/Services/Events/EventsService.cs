using Events.Application.DTOs.Events;
using System.ComponentModel.DataAnnotations;
using Events.Domain.Consts;
using Events.Application.DTOs.Events.Incoming;
using Events.Application.DTOs.Events.Results;
using Events.Domain.Exceptions;
using Events.Domain.Models;
using Events.Application.Repositories.Events;
using Events.Application.Caching;
using System.Text.Json;

namespace Events.Application.Services.Events;

public class EventsService(IEventRepository eventRepository, ICacheService cache) : IEventService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<EventDto> GetEventById(Guid id)
    {
        var key = $"event:{id}";
        var cached = await ReadCacheAsync<EventDto>(key);
        if (cached is not null)
            return cached;

        var eventData = await eventRepository.GetByIdAsync(id)
            ?? throw new NotFoundException(ErrorsMessages.EventNotFound);
        var result = eventData.AsDto();
        await cache.SetAsync(key, JsonSerializer.Serialize(result), CacheTtl);
        return result;
    }

    public async Task<List<EventDto>> GetTopEvents()
    {
        const string key = "events:top10";
        var cached = await ReadCacheAsync<List<EventDto>>(key);
        if (cached is not null)
            return cached;

        var events = await eventRepository.GetTopEventsAsync();
        var result = events.Select(e => e.AsDto()).ToList();
        await cache.SetAsync(key, JsonSerializer.Serialize(result), CacheTtl);
        return result;
    }

    private async Task<T?> ReadCacheAsync<T>(string key) where T : class
    {
        var value = await cache.GetAsync(key);
        if (value is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(value);
        }
        catch (JsonException)
        {
            // An invalid cached payload must not prevent reading the source of truth.
            await cache.RemoveAsync(key);
            return null;
        }
    }

    public async Task<EventDto> CreateEvent(CreateEventDto eventData)
    {
        eventData = NormalizeDates(eventData);
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);

        if (await eventRepository.ExistsByTitleAsync(eventData.Title))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var newEvent = Event.Create(Guid.NewGuid(), eventData.Title, eventData.Description, eventData.StartAt,
            eventData.EndAt, eventData.TotalSeats);

        await eventRepository.AddAsync(newEvent);
        return newEvent.AsDto();
    }

    public async Task<EventDto> UpdateEvent(Guid id, CreateEventDto eventData)
    {
        eventData = NormalizeDates(eventData);
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);

        var eventFound = await eventRepository.GetByIdAsync(id);
        if (eventFound is not null)
        {
            if (!eventFound.TryUpdateCapacity(eventData.TotalSeats))
                throw new ValidationException(ErrorsMessages.EventCapacityCannotBeLessThanReservedSeats);

            eventFound.Title = eventData.Title;
            eventFound.StartAt = eventData.StartAt;
            eventFound.EndAt = eventData.EndAt;
            eventFound.Description = eventData.Description;
            await eventRepository.SaveChangesAsync();
            await cache.RemoveAsync($"event:{id}");
            return eventFound.AsDto();
        }

        throw new NotFoundException(ErrorsMessages.EventNotFound);
    }

    public async Task<EventDto> UpdateEvent(Guid id, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle) || newTitle.Length < 5)
            throw new ValidationException(ErrorsMessages.EventTitleIsShort);

        if (await eventRepository.ExistsByTitleAsync(newTitle))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var eventFound = await eventRepository.GetByIdAsync(id);
        if (eventFound is not null)
        {
            eventFound.Title = newTitle;
            await eventRepository.SaveChangesAsync();
            await cache.RemoveAsync($"event:{id}");
            return eventFound.AsDto();
        }

        throw new NotFoundException(ErrorsMessages.EventNotFound);
    }

    public async Task DeleteEvent(Guid id)
    {
        var eventFound = await eventRepository.GetByIdAsync(id) ?? throw new NotFoundException(ErrorsMessages.EventNotFound);
        if (eventFound.HeldSeats > 0 || eventFound.AvailableSeats != eventFound.TotalSeats)
            throw new ConflictException("Cannot delete an event with reserved seats.");
        await eventRepository.RemoveAsync(eventFound);
        await cache.RemoveAsync($"event:{id}");
    }

    public async Task<PaginatedResult<EventDto>> GetEventsByFilters(GetEventsByFiltersDto filters)
    {
        if (filters.Page < 1 || filters.PageSize < 1)
            throw new ValidationException("Pagination parameters must be greater than or equal to 1.");

        var (events, totalItems) = await eventRepository.GetByFiltersAsync(filters);
        var items = events.Select(e => e.AsDto()).ToList();
        var totalPages = (int)Math.Ceiling((double)totalItems / filters.PageSize);

        return new PaginatedResult<EventDto>(items, filters.Page, filters.PageSize, totalItems, totalPages);
    }

    private static CreateEventDto NormalizeDates(CreateEventDto eventData) => eventData with
    {
        StartAt = ToUtc(eventData.StartAt, nameof(eventData.StartAt)),
        EndAt = ToUtc(eventData.EndAt, nameof(eventData.EndAt))
    };

    private static DateTime ToUtc(DateTime value, string field)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            throw new ValidationException($"{field} must include a time zone: use Z or an explicit offset such as +03:00.");

        return value.ToUniversalTime();
    }

    private static bool ValidateEventDto(CreateEventDto eventData, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(eventData.Title) || eventData.Title.Length < 5)
        {
            errorMessage = ErrorsMessages.EventTitleIsShort;
            return true;
        }

        if (eventData.StartAt < DateTime.UtcNow)
        {
            errorMessage = ErrorsMessages.CannotCreateEventInThePast;
            return true;
        }

        if (eventData.StartAt >= eventData.EndAt)
        {
            errorMessage = ErrorsMessages.CannotCreateEventWithStartLaterThenEnd;
            return true;
        }

        if (eventData.TotalSeats <= 0)
        {
            errorMessage = ErrorsMessages.EventTotalSeatsMustBePositive;
            return true;
        }

        errorMessage = string.Empty;
        return false;
    }
}
