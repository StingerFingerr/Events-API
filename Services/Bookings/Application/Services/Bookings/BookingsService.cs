using Bookings.Application.DTOs.Bookings.Results;
using Bookings.Application.Repositories.Bookings;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;

namespace Bookings.Application.Services.Bookings;

public sealed class BookingsService(IBookingRepository bookings) : IBookingService
{
    public Task<BookingDto> CreateBookingAsync(Guid eventId, Guid userId) =>
        bookings.RunBookingChangeAsync(async () =>
        {
            if (await bookings.CountActiveAsync(userId) >= 10)
                throw new ActiveBookingLimitExceededException(10);
            var booking = Booking.Create(eventId, userId);
            await bookings.AddAsync(booking);
            return BookingDto.FromBooking(booking);
        });

    public Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, bool isAdmin) =>
        bookings.RunBookingChangeAsync(async () =>
        {
            var booking = await FindAuthorized(bookingId, userId, isAdmin);
            booking.Cancel();
            await bookings.SaveChangesAsync();
            return BookingDto.FromBooking(booking);
        });

    public async Task<BookingDto> GetBookingByIdAsync(Guid bookingId, Guid userId, bool isAdmin) =>
        BookingDto.FromBooking(await FindAuthorized(bookingId, userId, isAdmin));

    private async Task<Booking> FindAuthorized(Guid id, Guid userId, bool isAdmin)
    {
        var booking = await bookings.GetByIdAsync(id) ?? throw new NotFoundException("Booking not found.");
        if (!isAdmin && booking.UserId != userId) throw new InsufficientPermissionsException();
        return booking;
    }
}
