using System.Collections.Concurrent;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Events_API.Background_tasks.Booking;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Test;

public class BookingsServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_SavesPendingBookingWithNewIdAndCurrentDate()
    {
        var eventId = Guid.NewGuid();
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, new Event(eventId, "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100))
        });
        var bookings = new ConcurrentDictionary<Guid, Booking>();
        var queue = new Mock<IBookingTaskQueue>();
        var service = CreateService(new InMemoryBookingsRepository(bookings), new InMemoryEventsRepository(events), queue.Object);
        var beforeCreation = DateTime.UtcNow;

        var booking = await service.CreateBookingAsync(eventId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, beforeCreation, DateTime.UtcNow);
        Assert.True(bookings.TryGetValue(booking.Id, out var savedBooking));
        Assert.Same(booking, savedBooking);
        queue.Verify(q => q.Enqueue(It.Is<BookingTask>(task =>
            task.BookingId == booking.Id && task.EventId == eventId)), Times.Once);
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
    public async Task CreateBookingAsync_ForSameEvent_AssignsUniqueIds()
    {
        var eventId = Guid.NewGuid();
        var service = CreateServiceWithEvent(eventId);

        var bookings = await Task.WhenAll(
            service.CreateBookingAsync(eventId),
            service.CreateBookingAsync(eventId),
            service.CreateBookingAsync(eventId));

        Assert.Equal(3, bookings.Select(booking => booking.Id).Distinct().Count());
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
        IEventsRepository eventsRepository,
        IBookingTaskQueue? bookingTaskQueue = null) =>
        new(bookingsRepository, eventsRepository, bookingTaskQueue ?? Mock.Of<IBookingTaskQueue>(), NullLogger<BookingsService>.Instance);

    private static Event CreateEvent(Guid eventId) =>
        new(eventId, "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
}
