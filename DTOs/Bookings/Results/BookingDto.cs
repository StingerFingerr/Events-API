using Events_API.Models;

namespace Events_API.DTOs.Bookings.Results;

public record BookingDto(Guid Id, Guid EventId, BookingStatus Status)
{
    public static BookingDto FromBooking(Booking booking) =>
        new(booking.Id, booking.EventId, booking.Status);
}
