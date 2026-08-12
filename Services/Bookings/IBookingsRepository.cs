using System.Collections.Concurrent;
using Events_API.Models;

namespace Events_API.Services.Bookings;

public interface IBookingsRepository
{
    ConcurrentDictionary<Guid, Booking> Bookings { get; }
    Guid NewBookingId { get; }
}
