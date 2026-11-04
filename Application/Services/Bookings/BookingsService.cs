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
    private const int MaxActiveBookingsPerUser = 10;
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    public Task<BookingDto> CreateBookingAsync(Guid eventId, Guid userId) =>
        RunBookingChangeAsync(() => ReserveSeatAndCreateBookingAsync(eventId, userId));

    private async Task<BookingDto> ReserveSeatAndCreateBookingAsync(Guid eventId, Guid userId)
    {
        var eventFound = await eventRepository.GetByIdWithReloadAsync(eventId)
            ?? throw new NotFoundException(ErrorsMessages.EventNotFound);
        var booking = Booking.Create(eventFound, userId);
        if (await bookingRepository.CountActiveAsync(userId) >= MaxActiveBookingsPerUser)
            throw new ActiveBookingLimitExceededException();
        if (!eventFound.TryReserveSeats())
            throw new NoAvailableSeatsException();

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

    public Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, UserRole role) =>
        RunBookingChangeAsync(() => CancelBookingAndReleaseSeatAsync(bookingId, userId, role));

    private async Task<BookingDto> CancelBookingAndReleaseSeatAsync(Guid bookingId, Guid userId, UserRole role)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException(ErrorsMessages.BookingNotFound);
        if (role != UserRole.Admin && booking.UserId != userId)
            throw new InsufficientPermissionsException();
        var wasActive = booking.Status is BookingStatus.Pending or BookingStatus.Confirmed;
        booking.Cancel();
        if (wasActive)
        {
            var reservedEvent = await eventRepository.GetByIdWithReloadAsync(booking.EventId)
                ?? throw new NotFoundException(ErrorsMessages.EventNotFound);
            reservedEvent.ReleaseSeats();
        }
        await bookingRepository.SaveChangesAsync();
        return BookingDto.FromBooking(booking);
    }

    private async Task<BookingDto> RunBookingChangeAsync(Func<Task<BookingDto>> change)
    {
        await BookingSemaphore.WaitAsync();
        try
        {
            return await bookingRepository.RunBookingChangeAsync(change);
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<BookingDto> GetBookingByIdAsync(Guid bookingId, Guid userId, UserRole role)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId) ?? throw new NotFoundException(ErrorsMessages.BookingNotFound);
        if (role != UserRole.Admin && booking.UserId != userId)
            throw new InsufficientPermissionsException();
        return BookingDto.FromBooking(booking);
    }

    public async Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        var booking = await bookingRepository.GetByIdAsync(bookingId) ?? throw new NotFoundException(ErrorsMessages.BookingNotFound);
        if (booking.Status == BookingStatus.Cancelled)
            throw new ConflictException("Booking is already cancelled.");
        switch (status)
        {
            case BookingStatus.Cancelled:
                throw new InsufficientPermissionsException();
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
