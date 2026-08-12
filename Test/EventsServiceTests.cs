using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services;
using Events_API.Services.Events;
using Moq;

namespace Test;

public class EventsFiltersTests
{
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1, -5)]
    [InlineData(0, 7)]
    public void GetEventsByFilters_WithInvalidPagination_ThrowsArgumentException(int page, int pageSize)
    {
        var repositoryMock = new Mock<IEventsRepository>();
        var service = new EventsService(repositoryMock.Object);
        var invalidFilters = new GetEventsByFiltersDto { Page = page, PageSize = pageSize };

        Assert.Throws<ValidationException>(() => service.GetEventsByFilters(invalidFilters));
    }

    [Fact]
    public void CreateEvent_ShouldSaveNewEventInRepositoryWithUniqueId()
    {
        var events = new ConcurrentDictionary<Guid, Event>();
        var mockRepository = new Mock<IEventsRepository>();
        var eventsService = new EventsService(mockRepository.Object);

        var newEventDto = new CreateEventDto()
        {
            Title = "test title",
            StartAt = DateTime.Now.AddDays(3),
            EndAt = DateTime.Now.AddDays(4),
        };

        var expectedId = Guid.NewGuid();
        mockRepository.Setup(r => r.NewEventId).Returns(expectedId);
        mockRepository.Setup(r => r.Events).Returns(events);

        eventsService.CreateEvent(newEventDto);

        bool containsSavedEvent = events.ContainsKey(expectedId);

        Assert.True(containsSavedEvent, "Событие было добавлено в репозиторий под сгенерированным Id");
        Assert.Equal("test title", events[expectedId].Title);
    }

    [Fact]
    public void FilterByTitle_ReturnsMatchingEvents()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var searchTitle = "festival";
        var expectedEvents = new List<string>() { "rock festival", "food festival" };
        var notExpectedResult = "rap concert";
        var filterByTitle = new GetEventsByFiltersDto() { Title = searchTitle };

        var result = eventService.GetEventsByFilters(filterByTitle).Items;

        Assert.DoesNotContain(notExpectedResult, result.Select(events => events.Title));
        Assert.Equal(expectedEvents, result.Select(events => events.Title));
    }

    [Fact]
    public void FilterByStartDate_ReturnsMatchingEvents()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var searchStartDate = DateTime.Now.AddDays(10);
        var expectedEventId = EventIds.FoodFestival;
        var filter = new GetEventsByFiltersDto() { From = searchStartDate };

        var result = eventService.GetEventsByFilters(filter);
        var eventFiltered = result.Items.FirstOrDefault();

        Assert.Single(result.Items);
        Assert.NotNull(eventFiltered);
        Assert.Equal(expectedEventId, eventFiltered.Id);
    }

    [Fact]
    public void FilterByEndDate_ReturnsMatchingEvents()
    {
        var (eventService, _, events) = CreateServiceWithDefaultEvents();
        var searchEndDate = DateTime.Now.AddDays(10);
        var expectedFirst = events[EventIds.RockFestival];
        var expectedSecond = events[EventIds.RapConcert];
        var filter = new GetEventsByFiltersDto() { To = searchEndDate };

        var result = eventService.GetEventsByFilters(filter);

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
    public void FilterByTitleFromTo_ReturnsMatchingEvents(string searchTitle, int daysFrom, int daysTo, string expectedEventId)
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

        var result = eventService.GetEventsByFilters(filter);

        Assert.Equal(Guid.Parse(expectedEventId), result.Items.First().Id);
    }

    [Fact]
    public void SuccessCreateEvent_AddsEventInRepository()
    {
        var (eventService, repository, _) = CreateServiceWithDefaultEvents();
        var newEvent = new CreateEventDto()
        {
            Title = "marathon",
            StartAt = DateTime.Now.AddDays(7),
            EndAt = DateTime.Now.AddDays(7).AddHours(8)
        };

        var result = eventService.CreateEvent(newEvent);

        Assert.NotNull(result);
        Assert.True(repository.Events.ContainsKey(result.Id));
    }

    [Fact]
    public void SuccessUpdateEventTitle_UpdatesEventInRepository()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var updateId = EventIds.FoodFestival;
        var newTitle = "sport marathon";

        var result = eventService.UpdateEvent(updateId, newTitle);

        Assert.NotNull(result);
        Assert.Equal(newTitle, result.Title);
    }

    [Fact]
    public void SuccessDeleteEvent_DeletesEventInRepository()
    {
        var (eventService, repository, _) = CreateServiceWithDefaultEvents();
        var deleteId = EventIds.FoodFestival;

        eventService.DeleteEvent(deleteId);

        Assert.False(repository.Events.ContainsKey(deleteId));
    }

    [Fact]
    public void WrongIdGetEvent_ThrowsNotFoundException()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var wrongId = Guid.NewGuid();

        Assert.Throws<NotFoundException>(() => eventService.GetEventById(wrongId));
    }

    [Fact]
    public void WrongIdUpdateEvent_ThrowsNotFoundException()
    {
        var (eventService, repository, _) = CreateServiceWithDefaultEvents();
        var wrongId = Guid.NewGuid();

        Assert.Throws<NotFoundException>(() => eventService.UpdateEvent(wrongId, "new title"));
        Assert.False(repository.Events.ContainsKey(wrongId));
    }

    [Fact]
    public void WrongTitleCreateEvent_ThrowsValidationException()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var createDto = new CreateEventDto()
        {
            Title = "xxx",
            StartAt = DateTime.Now,
            EndAt = DateTime.Now.AddDays(1),
        };

        Assert.Throws<ValidationException>(() => eventService.CreateEvent(createDto));
    }

    [Fact]
    public void UpdateEventStartDateToPast_ReturnsFalseResult()
    {
        var (eventService, _, _) = CreateServiceWithDefaultEvents();
        var dateInPast = new CreateEventDto()
        {
            Title = "new title",
            StartAt = DateTime.Now.AddDays(-1),
            EndAt = DateTime.Now.AddDays(7),
        };
        var eventId = EventIds.RockFestival;

        Assert.Throws<ValidationException>(() => eventService.UpdateEvent(eventId, dateInPast));
    }

    private static (IEventService EventService, IEventsRepository Repository, ConcurrentDictionary<Guid, Event> Events)
        CreateServiceWithDefaultEvents()
    {
        var events = new ConcurrentDictionary<Guid, Event>
        {
            [EventIds.RockFestival] = new(EventIds.RockFestival, "rock festival", DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)),
            [EventIds.RapConcert] = new(EventIds.RapConcert, "rap concert", DateTime.Now.AddDays(5), DateTime.Now.AddDays(7)),
            [EventIds.FoodFestival] = new(EventIds.FoodFestival, "food festival", DateTime.Now.AddDays(14), DateTime.Now.AddDays(15))
        };
        IEventsRepository repository = new InMemoryEventsRepository(events);

        return (new EventsService(repository), repository, events);
    }
}

internal static class EventIds
{
    public static readonly Guid RockFestival = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid RapConcert = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FoodFestival = Guid.Parse("33333333-3333-3333-3333-333333333333");
}
