using Events_API.Repositories.Events;
using System.ComponentModel.DataAnnotations;
using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;
using Events_API.Exceptions;
using Events_API.Models;

namespace Events_API.Services.Events;

public class EventsService(IEventRepository eventRepository) : IEventService
{
    public async Task<EventDto> GetEventById(Guid id)
    {
        var eventData = await eventRepository.GetByIdAsync(id);
        if (eventData is not null)
            return eventData.AsDto();
        throw new NotFoundException();
    }

    public async Task<EventDto> CreateEvent(CreateEventDto eventData)
    {
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
            return eventFound.AsDto();
        }

        throw new NotFoundException();
    }

    public async Task<EventDto> UpdateEvent(Guid id, string newTitle)
    {
        if (await eventRepository.ExistsByTitleAsync(newTitle))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var eventFound = await eventRepository.GetByIdAsync(id);
        if (eventFound is not null)
        {
            eventFound.Title = newTitle;
            await eventRepository.SaveChangesAsync();
            return eventFound.AsDto();
        }

        throw new NotFoundException();
    }

    public async Task DeleteEvent(Guid id)
    {
        var eventFound = await eventRepository.GetByIdAsync(id) ?? throw new NotFoundException();
        await eventRepository.RemoveAsync(eventFound);
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

    private static bool ValidateEventDto(CreateEventDto eventData, out string errorMessage)
    {
        if (eventData.Title.Length <= 3)
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
