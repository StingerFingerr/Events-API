using System.Collections.Concurrent;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Test;

public class BookingsServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_SavesPendingBookingWithNewIdAndCurrentDate()
    {
        var eventId = Guid.NewGuid();
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, Event.Create(eventId, "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100))
        });
        var bookings = new ConcurrentDictionary<Guid, Booking>();
        var service = CreateService(new InMemoryBookingsRepository(bookings), new InMemoryEventsRepository(events));
        var beforeCreation = DateTime.UtcNow;

        var booking = await service.CreateBookingAsync(eventId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, beforeCreation, DateTime.UtcNow);
        Assert.True(bookings.TryGetValue(booking.Id, out var savedBooking));
        Assert.Same(booking, savedBooking);
        Assert.Equal(99, events[eventId].AvailableSeats);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsSavedBooking()
    {
        var booking = new Booking { Id = Guid.NewGuid(), EventId = Guid.NewGuid() };
        var service = CreateService(
            new InMemoryBookingsRepository(new ConcurrentDictionary<Guid, Booking>(new[]
            {
                new KeyValuePair<Guid, Booking>(booking.Id, booking)
            })),
            new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>()));

        var result = await service.GetBookingByIdAsync(booking.Id);

        Assert.Same(booking, result);
    }

    [Fact]
    public async Task CreateBookingAsync_TenConcurrentRequests_AssignsUniqueIdsAndUsesAllSeats()
    {
        var eventId = Guid.NewGuid();
        var eventData = CreateEvent(eventId, totalSeats: 10);
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, eventData)
        });
        var service = CreateService(new InMemoryBookingsRepository(), new InMemoryEventsRepository(events));

        var bookingTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => service.CreateBookingAsync(eventId)));
        var bookings = await Task.WhenAll(bookingTasks);

        Assert.Equal(10, bookings.Length);
        Assert.Equal(10, bookings.Select(booking => booking.Id).Distinct().Count());
        Assert.Equal(0, eventData.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_TwentyConcurrentRequestsForFiveSeats_PreventsOverbooking()
    {
        var eventId = Guid.NewGuid();
        var eventData = CreateEvent(eventId, totalSeats: 5);
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, eventData)
        });
        var service = CreateService(new InMemoryBookingsRepository(), new InMemoryEventsRepository(events));

        var bookingTasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await service.CreateBookingAsync(eventId);
                    return true;
                }
                catch (NoAvailableSeatsException)
                {
                    return false;
                }
            }));
        var results = await Task.WhenAll(bookingTasks);

        Assert.Equal(5, results.Count(success => success));
        Assert.Equal(15, results.Count(success => !success));
        Assert.Equal(0, eventData.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_ForMissingEvent_ThrowsNotFoundException()
    {
        var service = CreateService(
            new InMemoryBookingsRepository(),
            new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>()));

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBookingAsync_WhenNoSeatsAreAvailable_ThrowsNoAvailableSeatsException()
    {
        var eventId = Guid.NewGuid();
        var eventData = Event.Create(eventId, "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 1);
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, eventData)
        });
        var bookings = new ConcurrentDictionary<Guid, Booking>();
        var service = CreateService(new InMemoryBookingsRepository(bookings), new InMemoryEventsRepository(events));

        await service.CreateBookingAsync(eventId);

        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(() => service.CreateBookingAsync(eventId));

        Assert.Equal("No available seats for this event", exception.Message);
        Assert.Equal(0, eventData.AvailableSeats);
        Assert.Single(bookings);
    }

    [Fact]
    public async Task CreateBookingAsync_ForDeletedEvent_ThrowsNotFoundException()
    {
        var eventId = Guid.NewGuid();
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, CreateEvent(eventId))
        });
        var service = CreateService(new InMemoryBookingsRepository(), new InMemoryEventsRepository(events));
        events.TryRemove(eventId, out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(eventId));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ForMissingBooking_ThrowsNotFoundException()
    {
        var service = CreateService(
            new InMemoryBookingsRepository(),
            new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>()));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBookingByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_UpdatesStatusAndProcessedAt()
    {
        var booking = new Booking { Id = Guid.NewGuid(), EventId = Guid.NewGuid(), Status = BookingStatus.Pending };
        var service = CreateService(
            new InMemoryBookingsRepository(new ConcurrentDictionary<Guid, Booking>(new[]
            {
                new KeyValuePair<Guid, Booking>(booking.Id, booking)
            })),
            new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>()));
        var beforeUpdate = DateTime.UtcNow;

        await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Confirmed);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.InRange(booking.ProcessedAt.Value, beforeUpdate, DateTime.UtcNow);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task GetBookingByIdAsync_ReturnsChangedProcessingStatus(BookingStatus finalStatus)
    {
        var eventId = Guid.NewGuid();
        var service = CreateServiceWithEvent(eventId);

        var created = await service.CreateBookingAsync(eventId);
        var pending = await service.GetBookingByIdAsync(created.Id);
        Assert.Equal(BookingStatus.Pending, pending.Status);

        await service.UpdateBookingStatusAsync(created.Id, finalStatus);
        var processed = await service.GetBookingByIdAsync(created.Id);

        Assert.Equal(finalStatus, processed.Status);
    }

    private static BookingsService CreateServiceWithEvent(Guid eventId)
    {
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, CreateEvent(eventId))
        });

        return CreateService(new InMemoryBookingsRepository(), new InMemoryEventsRepository(events));
    }

    private static BookingsService CreateService(
        IBookingsRepository bookingsRepository,
        IEventsRepository eventsRepository) =>
        new(bookingsRepository, eventsRepository, NullLogger<BookingsService>.Instance);

    private static Event CreateEvent(Guid eventId, int totalSeats = 100) =>
        Event.Create(eventId, "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), totalSeats);
}
