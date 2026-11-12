using Bookings.Application.DTOs.Bookings.Results;
using Bookings.Domain.Models;

namespace Bookings.Application.Services.Bookings;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(Guid eventId, Guid userId);
    Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, bool isAdmin);
    Task<BookingDto> GetBookingByIdAsync(Guid bookingId, Guid userId, bool isAdmin);
}
