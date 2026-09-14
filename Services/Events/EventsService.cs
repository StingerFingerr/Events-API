using Events_API.DataAccess;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.DTOs.Events.Results;
using Events_API.Exceptions;
using Events_API.Models;

namespace Events_API.Services.Events;

public class EventsService(AppDbContext context) : IEventService
{
    public async Task<EventDto> GetEventById(Guid id)
    {
        var eventData = await context.Events.FindAsync(id);
        if (eventData is not null)
            return eventData.AsDto();
        throw new NotFoundException();
    }

    public async Task<EventDto> CreateEvent(CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);

        if (await EventExistsByTitle(eventData.Title))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var newEvent = Event.Create(Guid.NewGuid(), eventData.Title, eventData.Description, eventData.StartAt,
            eventData.EndAt, eventData.TotalSeats);

        context.Events.Add(newEvent);
        await context.SaveChangesAsync();
        return newEvent.AsDto();
    }

    public async Task<EventDto> UpdateEvent(Guid id, CreateEventDto eventData)
    {
        if (ValidateEventDto(eventData, out var errorMessage))
            throw new ValidationException(errorMessage);

        var eventFound = await context.Events.FindAsync(id);
        if (eventFound is not null)
        {
            if (!eventFound.TryUpdateCapacity(eventData.TotalSeats))
                throw new ValidationException(ErrorsMessages.EventCapacityCannotBeLessThanReservedSeats);

            eventFound.Title = eventData.Title;
            eventFound.StartAt = eventData.StartAt;
            eventFound.EndAt = eventData.EndAt;
            eventFound.Description = eventData.Description;
            await context.SaveChangesAsync();
            return eventFound.AsDto();
        }

        throw new NotFoundException();
    }

    public async Task<EventDto> UpdateEvent(Guid id, string newTitle)
    {
        if (await EventExistsByTitle(newTitle))
            throw new ConflictException(ErrorsMessages.EventAlreadyExists);

        var eventFound = await context.Events.FindAsync(id);
        if (eventFound is not null)
        {
            eventFound.Title = newTitle;
            await context.SaveChangesAsync();
            return eventFound.AsDto();
        }

        throw new NotFoundException();
    }

    public async Task DeleteEvent(Guid id)
    {
        var eventFound = await context.Events.FindAsync(id) ?? throw new NotFoundException();
        context.Events.Remove(eventFound);
        await context.SaveChangesAsync();
    }

    public async Task<PaginatedResult<EventDto>> GetEventsByFilters(GetEventsByFiltersDto filters)
    {
        if (filters.Page < 1 || filters.PageSize < 1)
            throw new ValidationException("Pagination parameters must be greater than or equal to 1.");

        var filtered = context.Events.AsNoTracking();

        if (filters.Title is not null)
            filtered = filtered.Where(e => e.Title.ToLower().Contains(filters.Title.ToLower()));
        if (filters.From is not null)
            filtered = filtered.Where(e => e.StartAt >= filters.From);
        if (filters.To is not null)
            filtered = filtered.Where(e => e.StartAt <= filters.To);

        var items = await filtered
            .OrderBy(e => e.StartAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(e => e.AsDto())
            .ToListAsync();

        var totalItems = await filtered.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalItems / filters.PageSize);

        return new PaginatedResult<EventDto>(items, filters.Page, filters.PageSize, totalItems, totalPages);
    }

    private Task<bool> EventExistsByTitle(string title) =>
        context.Events.AnyAsync(e => e.Title.ToLower() == title.ToLower());

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
