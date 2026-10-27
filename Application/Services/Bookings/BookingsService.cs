using Application.DTOs.Bookings.Results;
using Domain.Consts;
using Microsoft.Extensions.Logging;
using Application.Repositories.Bookings;
using Application.Repositories.Events;
using Domain.Exceptions;
using Domain.Models;

namespace Application.Services.Bookings;

public class BookingsService(IEventRepository eventRepository, IBookingRepository bookingRepository, ILogger<BookingsService> logger) : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public async Task<BookingDto> CreateBookingAsync(Guid eventId)
    {
        await BookingSemaphore.WaitAsync();
        try
        {
            var eventFound = await eventRepository.GetByIdWithReloadAsync(eventId) ?? throw new NotFoundException(ErrorsMessages.EventNotFound);
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
            return BookingDto.FromBooking(booking);
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<BookingDto> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId) ?? throw new NotFoundException(ErrorsMessages.BookingNotFound);
        return BookingDto.FromBooking(booking);
    }

    public async Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId) ?? throw new NotFoundException(ErrorsMessages.BookingNotFound);
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
