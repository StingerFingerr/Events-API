using System.ComponentModel.DataAnnotations;
using Events_API.Consts;
using Events_API.DTOs.Events;
using Events_API.DTOs.Events.Incoming;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services;
using Moq;

namespace Test;

public class EventsFiltersTests
{
    private IEventService _eventService;
    private readonly IEventsRepository _repository;
    private List<Event> _events;

    public EventsFiltersTests()
    {
        _events = [
            new()
            {
                Id = 1, Title = "rock festival",
                StartAt = DateTime.Now.AddDays(1),
                EndAt = DateTime.Now.AddDays(2)
            },
            new()
            {
                Id = 2, Title = "rap concert",
                StartAt = DateTime.Now.AddDays(5),
                EndAt = DateTime.Now.AddDays(7)
            },
            new()
            {
                Id = 3, Title = "food festival",
                StartAt = DateTime.Now.AddDays(14),
                EndAt = DateTime.Now.AddDays(15)
            },
        ];
        _repository = new InMemoryEventsRepository(_events);

        _eventService = new EventsService(_repository);
    }

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
    public void CreateEvent_ShouldCallNewEventIdOnce()
    {
        var mockRepository = new Mock<IEventsRepository>();
        var eventsService = new EventsService(mockRepository.Object);
        var newEvent = new CreateEventDto()
        {
            Title = "new title",
            StartAt = DateTime.Now.AddDays(1),
            EndAt = DateTime.Now.AddDays(7),
        };
        mockRepository.Setup<List<Event>>(r => r.Events).Returns(_events);

        eventsService.CreateEvent(newEvent);

        mockRepository.Verify(r => r.NewEventId, Times.Once);
    }

    [Fact]
    public void FilterByTitle_ReturnsMatchingEvents()
    {
        var searchTitle = "festival";
        var expectedEvents = new List<string>() { "rock festival", "food festival" };
        var notExpectedResult = "rap concert";
        var filterByTitle = new GetEventsByFiltersDto() { Title = searchTitle };

        var result = _eventService.GetEventsByFilters(filterByTitle).Events;

        Assert.DoesNotContain(notExpectedResult, result.Select(events => events.Title));
        Assert.Equal(expectedEvents, result.Select(events => events.Title));
    }

    [Fact]
    public void FilterByStartDate_ReturnsMatchingEvents()
    {
        var searchStartDate = DateTime.Now.AddDays(10);
        var expectedEventId = 3;
        var filter = new GetEventsByFiltersDto() { From = searchStartDate };

        var result = _eventService.GetEventsByFilters(filter);
        var eventFiltered = result.Events.FirstOrDefault();

        Assert.Single(result.Events);
        Assert.NotNull(eventFiltered);
        Assert.Equal(expectedEventId, eventFiltered.Id);
    }

    [Fact]
    public void SuccessCreateEvent_AddsEventInRepository()
    {
        var newEvent = new CreateEventDto()
        {
            Title = "marathon",
            StartAt = DateTime.Now.AddDays(7),
            EndAt = DateTime.Now.AddDays(7).AddHours(8)
        };

        var result = _eventService.CreateEvent(newEvent);

        Assert.NotNull(result);
        Assert.Contains(result.Id, _repository.Events.Select(e => e.Id));
    }

    [Fact]
    public void SuccessUpdateEventTitle_UpdatesEventInRepository()
    {
        var updateId = 3;
        var newTitle = "sport marathon";

        var result = _eventService.UpdateEvent(updateId, newTitle);

        Assert.NotNull(result);
        Assert.Equal(newTitle, result.Title);
    }

    [Fact]
    public void SuccessDeleteEvent_DeletesEventInRepository()
    {
        var deleteId = 3;

        _eventService.DeleteEvent(deleteId);

        Assert.DoesNotContain(deleteId, _repository.Events.Select(e => e.Id));
    }

    [Fact]
    public void WrongIdGetEvent_ThrowsNotFoundException()
    {
        var wrongId = 69;

        Assert.Throws<NotFoundException>(() => _eventService.GetEventById(wrongId));
    }

    [Fact]
    public void WrongIdUpdateEvent_ThrowsNotFoundException()
    {
        var wrongId = 69;

        Assert.Throws<NotFoundException>(() => _eventService.UpdateEvent(wrongId, "new title"));
        Assert.DoesNotContain(wrongId, _repository.Events.Select(e => e.Id));
    }

    [Fact]
    public void WrongTitleCreateEvent_ThrowsValidationException()
    {
        var createDto = new CreateEventDto()
        {
            Title = "xxx",
            StartAt = DateTime.Now,
            EndAt = DateTime.Now.AddDays(1),
        };

        Assert.Throws<ValidationException>(() => _eventService.CreateEvent(createDto));
    }

    [Fact]
    public void UpdateEventStartDateToPast_ReturnsFalseResult()
    {
        var dateInPast = new EventUpdateDto()
        {
            Title = "new title",
            StartAt = DateTime.Now.AddDays(-1),
            EndAt = DateTime.Now.AddDays(7),
        };
        var eventId = 1;
        
        Assert.Throws<ValidationException>(() => _eventService.UpdateEvent(eventId, dateInPast));
    }
}