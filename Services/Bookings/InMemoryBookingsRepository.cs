using System.Collections.Concurrent;
using Events_API.Models;

namespace Events_API.Services.Bookings;

public class InMemoryBookingsRepository(ConcurrentDictionary<Guid, Booking>? bookings = null) : IBookingsRepository
{
    public ConcurrentDictionary<Guid, Booking> Bookings { get; } = bookings ?? new ConcurrentDictionary<Guid, Booking>();
    public Guid NewBookingId => Guid.NewGuid();
}
