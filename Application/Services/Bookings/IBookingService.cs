using Application.DTOs.Bookings.Results;
using Domain.Models;

namespace Application.Services.Bookings;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(Guid eventId, Guid userId);
    Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, UserRole role);
    Task<BookingDto> GetBookingByIdAsync(Guid bookingId, Guid userId, UserRole role);
    Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status);
}
