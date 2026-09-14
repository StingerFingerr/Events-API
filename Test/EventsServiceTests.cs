using Events_API.DataAccess;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Events;

namespace Test;

public class EventsFiltersTests : IDisposable
{
    private readonly List<AppDbContext> _contexts = new();
    public void Dispose() { foreach (var context in _contexts) context.Dispose(); }
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1, -5)]
    [InlineData(0, 7)]
    public async Task GetEventsByFilters_WithInvalidPagination_ThrowsArgumentException(int page, int pageSize)
    {
        using var context = TestDatabase.Create();
        var service = new EventsService(context);
        var invalidFilters = new GetEventsByFiltersDto { Page = page, PageSize = pageSize };

        await Assert.ThrowsAsync<ValidationException>(() => service.GetEventsByFilters(invalidFilters));
    }

    [Fact]
    public async Task CreateEvent_ShouldSaveNewEventInRepositoryWithUniqueId()
    {
        var events = new ConcurrentDictionary<Guid, Event>();
        using var context = TestDatabase.Create();
        var eventsService = new EventsService(context);

        var newEventDto = new CreateEventDto()
        {
            Title = "test title",
            TotalSeats = 100,
            StartAt = DateTime.Now.AddDays(3),
            EndAt = DateTime.Now.AddDays(4),
        };

        var created = await eventsService.CreateEvent(newEventDto);
        context.ChangeTracker.Clear();
        var saved = await context.Events.SingleAsync();
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(created.Id, saved.Id);
        Assert.Equal("test title", saved.Title);
        Assert.Equal(100, saved.TotalSeats);
        Assert.Equal(100, saved.AvailableSeats);
    }

    [Fact]
    public async Task FilterByTitle_ReturnsMatchingEvents()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var searchTitle = "festival";
        var expectedEvents = new List<string>() { "rock festival", "food festival" };
        var notExpectedResult = "rap concert";
        var filterByTitle = new GetEventsByFiltersDto() { Title = searchTitle };

        var result = (await eventService.GetEventsByFilters(filterByTitle)).Items;

        Assert.DoesNotContain(notExpectedResult, result.Select(events => events.Title));
        Assert.Equal(expectedEvents, result.Select(events => events.Title));
    }

    [Fact]
    public async Task FilterByStartDate_ReturnsMatchingEvents()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var searchStartDate = DateTime.Now.AddDays(10);
        var expectedEventId = EventIds.FoodFestival;
        var filter = new GetEventsByFiltersDto() { From = searchStartDate };

        var result = await eventService.GetEventsByFilters(filter);
        var eventFiltered = result.Items.FirstOrDefault();

        Assert.Single(result.Items);
        Assert.NotNull(eventFiltered);
        Assert.Equal(expectedEventId, eventFiltered.Id);
    }

    [Fact]
    public async Task FilterByEndDate_ReturnsMatchingEvents()
    {
        var (eventService, _, events) = CreateServiceWithDefaultEvents();
        var searchEndDate = DateTime.Now.AddDays(10);
        var expectedFirst = events[EventIds.RockFestival];
        var expectedSecond = events[EventIds.RapConcert];
        var filter = new GetEventsByFiltersDto() { To = searchEndDate };

        var result = await eventService.GetEventsByFilters(filter);

        Assert.Equal(2, result.TotalItems);
        Assert.Collection(result.Items,
            firstElement =>
            {
                Assert.Equal(expectedFirst.Id, firstElement.Id);
            },
            secondElement =>
            {
                Assert.Equal(expectedSecond.Id, secondElement.Id);
            });
    }

    [Theory]
    [InlineData("festival", 10, 15, "33333333-3333-3333-3333-333333333333")]
    [InlineData("FESTIVAL", 11, 14, "33333333-3333-3333-3333-333333333333")]
    [InlineData("Concert", 1, 22, "22222222-2222-2222-2222-222222222222")]
    public async Task FilterByTitleFromTo_ReturnsMatchingEvents(string searchTitle, int daysFrom, int daysTo, string expectedEventId)
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var from = DateTime.Now.AddDays(daysFrom);
        var to = DateTime.Now.AddDays(daysTo);
        var filter = new GetEventsByFiltersDto()
        {
            Title = searchTitle,
            From = from,
            To = to
        };

        var result = await eventService.GetEventsByFilters(filter);

        Assert.Equal(Guid.Parse(expectedEventId), result.Items.First().Id);
    }

    [Fact]
    public async Task SuccessCreateEvent_AddsEventInRepository()
    {
        var (eventService, repository, _) = CreateServiceWithDefaultEvents();
        var newEvent = new CreateEventDto()
        {
            Title = "marathon",
            TotalSeats = 100,
            StartAt = DateTime.Now.AddDays(7),
            EndAt = DateTime.Now.AddDays(7).AddHours(8)
        };

        var result = await eventService.CreateEvent(newEvent);

        Assert.NotNull(result);
        Assert.True(await repository.Events.AnyAsync(e => e.Id == result.Id));
        Assert.Equal(100, result.TotalSeats);
        Assert.Equal(100, result.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateEvent_WithNonPositiveTotalSeats_ThrowsValidationException(int totalSeats)
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var eventData = new CreateEventDto
        {
            Title = "conference",
            TotalSeats = totalSeats,
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2)
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => eventService.CreateEvent(eventData));

        Assert.Equal(ErrorsMessages.EventTotalSeatsMustBePositive, exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EventCreate_WithNonPositiveTotalSeats_ThrowsArgumentException(int totalSeats)
    {
        Assert.Throws<ArgumentException>(() => Event.Create(
            Guid.NewGuid(), "conference", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), totalSeats));
    }

    [Fact]
    public async Task UpdateEventCapacity_PreservesReservedSeats()
    {
        var (eventService, _, events) = CreateServiceWithDefaultEvents();
        var eventData = events[EventIds.RockFestival];
        eventData.TryReserveSeats(10);
        var update = new CreateEventDto
        {
            Title = eventData.Title,
            TotalSeats = 120,
            StartAt = eventData.StartAt,
            EndAt = eventData.EndAt
        };

        var result = await eventService.UpdateEvent(eventData.Id, update);

        Assert.Equal(120, result.TotalSeats);
        Assert.Equal(110, result.AvailableSeats);
    }

    [Fact]
    public async Task UpdateEventCapacity_BelowReservedSeats_ThrowsValidationException()
    {
        var (eventService, _, events) = CreateServiceWithDefaultEvents();
        var eventData = events[EventIds.RockFestival];
        eventData.TryReserveSeats(10);
        var update = new CreateEventDto
        {
            Title = eventData.Title,
            TotalSeats = 9,
            StartAt = eventData.StartAt,
            EndAt = eventData.EndAt
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => eventService.UpdateEvent(eventData.Id, update));

        Assert.Equal(ErrorsMessages.EventCapacityCannotBeLessThanReservedSeats, exception.Message);
        Assert.Equal(100, eventData.TotalSeats);
        Assert.Equal(90, eventData.AvailableSeats);
    }

    [Fact]
    public async Task SuccessUpdateEventTitle_UpdatesEventInRepository()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var updateId = EventIds.FoodFestival;
        var newTitle = "sport marathon";

        var result = await eventService.UpdateEvent(updateId, newTitle);

        Assert.NotNull(result);
        Assert.Equal(newTitle, result.Title);
    }

    [Fact]
    public async Task SuccessDeleteEvent_DeletesEventInRepository()
    {
        var (eventService, repository, _) = CreateServiceWithDefaultEvents();
        var deleteId = EventIds.FoodFestival;

        await eventService.DeleteEvent(deleteId);

        Assert.False(await repository.Events.AnyAsync(e => e.Id == deleteId));
    }

    [Fact]
    public async Task WrongIdGetEvent_ThrowsNotFoundException()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var wrongId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => eventService.GetEventById(wrongId));
    }

    [Fact]
    public async Task WrongIdUpdateEvent_ThrowsNotFoundException()
    {
        var (eventService, repository, _) = CreateServiceWithDefaultEvents();
        var wrongId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => eventService.UpdateEvent(wrongId, "new title"));
        Assert.False(await repository.Events.AnyAsync(e => e.Id == wrongId));
    }

    [Fact]
    public async Task WrongTitleCreateEvent_ThrowsValidationException()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var createDto = new CreateEventDto()
        {
            Title = "xxx",
            TotalSeats = 100,
            StartAt = DateTime.Now,
            EndAt = DateTime.Now.AddDays(1),
        };

        await Assert.ThrowsAsync<ValidationException>(() => eventService.CreateEvent(createDto));
    }

    [Fact]
    public async Task UpdateEventStartDateToPast_ReturnsFalseResult()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var dateInPast = new CreateEventDto()
        {
            Title = "new title",
            TotalSeats = 100,
            StartAt = DateTime.Now.AddDays(-1),
            EndAt = DateTime.Now.AddDays(7),
        };
        var eventId = EventIds.RockFestival;

        await Assert.ThrowsAsync<ValidationException>(() => eventService.UpdateEvent(eventId, dateInPast));
    }

    private (IEventService EventService, AppDbContext Repository, ConcurrentDictionary<Guid, Event> Events)
        CreateServiceWithDefaultEvents()
    {
        var events = new ConcurrentDictionary<Guid, Event>
        {
            [EventIds.RockFestival] = Event.Create(EventIds.RockFestival, "rock festival", DateTime.Now.AddDays(1), DateTime.Now.AddDays(2), 100),
            [EventIds.RapConcert] = Event.Create(EventIds.RapConcert, "rap concert", DateTime.Now.AddDays(5), DateTime.Now.AddDays(7), 100),
            [EventIds.FoodFestival] = Event.Create(EventIds.FoodFestival, "food festival", DateTime.Now.AddDays(14), DateTime.Now.AddDays(15), 100)
        };
        var repository = TestDatabase.Create();
        _contexts.Add(repository);
        repository.Events.AddRange(events.Values);
        repository.SaveChanges();

        return (new EventsService(repository), repository, events);
    }
}

internal static class EventIds
{
    public static readonly Guid RockFestival = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid RapConcert = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FoodFestival = Guid.Parse("33333333-3333-3333-3333-333333333333");
}
