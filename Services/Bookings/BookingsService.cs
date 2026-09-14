using Events_API.DataAccess;
using Events_API.Exceptions;
using Events_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Events_API.Services.Bookings;

public class BookingsService(AppDbContext context, ILogger<BookingsService> logger) : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingSemaphore.WaitAsync();
        try
        {
            var eventFound = await context.Events.FindAsync(eventId) ?? throw new NotFoundException();
            await context.Entry(eventFound).ReloadAsync();
            if (context.Entry(eventFound).State == EntityState.Detached)
                throw new NotFoundException();
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
                context.Bookings.Add(booking);
                await context.SaveChangesAsync();
            }
            catch
            {
                context.Entry(booking).State = EntityState.Detached;
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
        await context.Bookings.FindAsync(bookingId) ?? throw new NotFoundException();

    public async Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        var booking = await context.Bookings.FindAsync(bookingId) ?? throw new NotFoundException();
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
        await context.SaveChangesAsync();
    }
}
