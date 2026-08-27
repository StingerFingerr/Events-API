using System.Collections.Concurrent;
using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Test;

public class BookingTests
{
    [Fact]
    public void Confirm_SetsConfirmedStatusAndProcessedAt()
    {
        var booking = new Booking { Status = BookingStatus.Pending };
        var beforeConfirmation = DateTime.UtcNow;

        booking.Confirm();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.InRange(booking.ProcessedAt.Value, beforeConfirmation, DateTime.UtcNow);
    }

    [Fact]
    public void Reject_SetsRejectedStatusAndProcessedAt()
    {
        var booking = new Booking { Status = BookingStatus.Pending };
        var beforeRejection = DateTime.UtcNow;

        booking.Reject();

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.InRange(booking.ProcessedAt.Value, beforeRejection, DateTime.UtcNow);
    }

    [Fact]
    public async Task RejectAndReleaseSeats_RestoresSeatAndAllowsAnotherBooking()
    {
        var eventId = Guid.NewGuid();
        var eventData = new Event(
            eventId,
            "concert",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            totalSeats: 1);
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, eventData)
        });
        var service = new BookingsService(
            new InMemoryBookingsRepository(),
            new InMemoryEventsRepository(events),
            NullLogger<BookingsService>.Instance);
        var firstBooking = await service.CreateBookingAsync(eventId);

        firstBooking.Reject();
        eventData.ReleaseSeats();

        Assert.Equal(1, eventData.AvailableSeats);

        var secondBooking = await service.CreateBookingAsync(eventId);

        Assert.Equal(BookingStatus.Pending, secondBooking.Status);
        Assert.NotEqual(firstBooking.Id, secondBooking.Id);
        Assert.Equal(0, eventData.AvailableSeats);
    }
}
