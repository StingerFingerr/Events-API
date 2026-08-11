using System.Collections.Concurrent;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;

namespace Test;

public class BookingsServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_SavesPendingBookingWithNewIdAndCurrentDate()
    {
        var eventId = Guid.NewGuid();
        var events = new ConcurrentDictionary<Guid, Event>(new[]
        {
            new KeyValuePair<Guid, Event>(eventId, new Event(eventId, "concert", DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)))
        });
        var bookings = new ConcurrentDictionary<Guid, Booking>();
        var service = new BookingsService(
            new InMemoryBookingsRepository(bookings),
            new InMemoryEventsRepository(events));
        var beforeCreation = DateTime.Now;

        var booking = await service.CreateBookingAsync(eventId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, beforeCreation, DateTime.Now);
        Assert.True(bookings.TryGetValue(booking.Id, out var savedBooking));
        Assert.Same(booking, savedBooking);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsSavedBooking()
    {
        var booking = new Booking { Id = Guid.NewGuid(), EventId = Guid.NewGuid() };
        var service = new BookingsService(
            new InMemoryBookingsRepository(new ConcurrentDictionary<Guid, Booking>(new[]
            {
                new KeyValuePair<Guid, Booking>(booking.Id, booking)
            })),
            new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>()));

        var result = await service.GetBookingByIdAsync(booking.Id);

        Assert.Same(booking, result);
    }

    [Fact]
    public async Task CreateBookingAsync_ForMissingEvent_ThrowsNotFoundException()
    {
        var service = new BookingsService(
            new InMemoryBookingsRepository(),
            new InMemoryEventsRepository(new ConcurrentDictionary<Guid, Event>()));

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid()));
    }
}
