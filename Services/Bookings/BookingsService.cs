using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Events;

namespace Events_API.Services.Bookings;

public class BookingsService(
    IBookingsRepository bookingsRepository,
    IEventsRepository eventsRepository,
    ILogger<BookingsService> logger) : IBookingService
{
    private readonly Lock _bookingLock = new();

    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            if (!eventsRepository.Events.TryGetValue(eventId, out var eventFound))
                throw new NotFoundException();

            if (!eventFound.TryReserveSeats())
                throw new NoAvailableSeatsException();

            var booking = new Booking
            {
                Id = bookingsRepository.NewBookingId,
                EventId = eventId,
                CreatedAt = DateTime.UtcNow,
                Status = BookingStatus.Pending,
                ReservedEvent = eventFound
            };

            try
            {
                if (!bookingsRepository.Bookings.TryAdd(booking.Id, booking))
                    throw new ConflictException("Unable to create booking.");

                logger.LogInformation("Pending booking {BookingId} was created", booking.Id);
            }
            catch
            {
                eventFound.ReleaseSeats();
                throw;
            }

            return Task.FromResult(booking);
        }
    }

    public Task<Booking> GetBookingByIdAsync(Guid bookingId)
    {
        if (bookingsRepository.Bookings.TryGetValue(bookingId, out var booking))
            return Task.FromResult(booking);

        throw new NotFoundException();
    }

    public Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        if (!bookingsRepository.Bookings.TryGetValue(bookingId, out var booking))
            throw new NotFoundException();

        switch (status)
        {
            case BookingStatus.Confirmed:
                booking.Confirm();
                break;
            case BookingStatus.Rejected:
                booking.Reject();
                break;
            default:
                booking.Status = BookingStatus.Pending;
                booking.ProcessedAt = null;
                break;
        }

        return Task.CompletedTask;
    }
}
