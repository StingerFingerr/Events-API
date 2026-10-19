using Domain.Models;

namespace Application.DTOs.Bookings.Results;

public record BookingDto(
    Guid Id,
    Guid EventId,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    BookingStatus Status)
{
    public static BookingDto FromBooking(Booking booking) =>
        new(booking.Id, booking.EventId, booking.CreatedAt, booking.ProcessedAt, booking.Status);
}
