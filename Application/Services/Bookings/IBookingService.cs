using Application.DTOs.Bookings.Results;
using Domain.Models;

namespace Application.Services.Bookings;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(Guid eventId);
    Task<BookingDto> GetBookingByIdAsync(Guid bookingId);
    Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status);
}
