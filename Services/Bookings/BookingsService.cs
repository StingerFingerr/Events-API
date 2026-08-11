using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Events;

namespace Events_API.Services.Bookings;

public class BookingsService(IBookingsRepository bookingsRepository, IEventsRepository eventsRepository) : IBookingService
{
    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        if (!eventsRepository.Events.ContainsKey(eventId))
            throw new NotFoundException();

        var booking = new Booking
        {
            Id = bookingsRepository.NewBookingId,
            EventId = eventId,
            CreatedAt = DateTime.Now,
            Status = BookingStatus.Pending
        };

        if (!bookingsRepository.Bookings.TryAdd(booking.Id, booking))
            throw new ConflictException("Unable to create booking.");

        return Task.FromResult(booking);
    }

    public Task<Booking> GetBookingByIdAsync(Guid bookingId)
    {
        if (bookingsRepository.Bookings.TryGetValue(bookingId, out var booking))
            return Task.FromResult(booking);

        throw new NotFoundException();
    }
}
