using Microsoft.Extensions.Logging;
using Application.Repositories.Bookings;
using Application.Repositories.Events;
using Domain.Exceptions;
using Domain.Models;

namespace Application.Services.Bookings;

public class BookingsService(IEventRepository eventRepository, IBookingRepository bookingRepository, ILogger<BookingsService> logger) : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingSemaphore.WaitAsync();
        try
        {
            var eventFound = await eventRepository.GetByIdWithReloadAsync(eventId) ?? throw new NotFoundException();
            if (!eventFound.TryReserveSeats())
                throw new NoAvailableSeatsException();

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Event = eventFound,
                CreatedAt = DateTime.UtcNow,
                Status = BookingStatus.Pending
            };
            try
            {
                await bookingRepository.AddAsync(booking);
            }
            catch
            {
                eventFound.ReleaseSeats();
                throw;
            }
            logger.LogInformation("Pending booking {BookingId} was created", booking.Id);
            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<Booking> GetBookingByIdAsync(Guid bookingId) =>
        await bookingRepository.GetByIdAsync(bookingId) ?? throw new NotFoundException();

    public async Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId) ?? throw new NotFoundException();
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
        await bookingRepository.SaveChangesAsync();
    }
}
