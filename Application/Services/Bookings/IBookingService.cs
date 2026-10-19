using Domain.Models;

namespace Application.Services.Bookings;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);
    Task<Booking> GetBookingByIdAsync(Guid bookingId);
    Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status);
}
