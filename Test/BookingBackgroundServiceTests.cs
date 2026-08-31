using System.Collections.Concurrent;
using Events_API.Background_tasks.Booking;
using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Test;

public class BookingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ConfirmsPendingBooking()
    {
        var eventId = Guid.NewGuid();
        var eventData = CreateEvent(eventId);
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            CreatedAt = DateTime.UtcNow,
            Status = BookingStatus.Pending
        };
        var bookings = new InMemoryBookingsRepository(new ConcurrentDictionary<Guid, Booking>(new[]
        {
            new KeyValuePair<Guid, Booking>(booking.Id, booking)
        }));
        var events = new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, eventData)
        }));
        var worker = new BookingBackgroundService(
            bookings,
            events,
            NullLogger<BookingBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitForStatusAsync(booking, BookingStatus.Confirmed);
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(booking.ProcessedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventWasDeleted_RejectsBookingAndReleasesSeat()
    {
        var eventId = Guid.NewGuid();
        var eventData = CreateEvent(eventId);
        var eventsDictionary = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, eventData)
        });
        var bookings = new InMemoryBookingsRepository();
        var events = new InMemoryEventsRepository(eventsDictionary);
        var bookingService = new BookingsService(bookings, events, NullLogger<BookingsService>.Instance);
        var booking = await bookingService.CreateBookingAsync(eventId);
        Assert.Equal(99, eventData.AvailableSeats);
        eventsDictionary.TryRemove(eventId, out _);

        var worker = new BookingBackgroundService(
            bookings,
            events,
            NullLogger<BookingBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitForStatusAsync(booking, BookingStatus.Rejected);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(100, eventData.AvailableSeats);
        Assert.NotNull(booking.ProcessedAt);
    }

    private static async Task WaitForStatusAsync(Booking booking, BookingStatus status)
    {
        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (booking.Status != status && DateTime.UtcNow < timeout)
            await Task.Delay(20);

        Assert.Equal(status, booking.Status);
    }

    private static Event CreateEvent(Guid eventId) =>
        Event.Create(eventId, "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
}
