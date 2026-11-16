using Bookings.Application.Services.Bookings;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;
using Bookings.Infrastructure.DataAccess;
using Bookings.Infrastructure.Repositories.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Test;

public class BookingsServiceRulesTests
{
    private static BookingsDbContext CreateContext() => new(new DbContextOptionsBuilder<BookingsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task TenActiveBookings_BlockEleventh(int confirmedCount)
    {
        await using var db = CreateContext();
        var user = Guid.NewGuid();
        for (var i = 0; i < 10; i++)
        {
            var booking = Booking.Create(Guid.NewGuid(), user);
            if (i < confirmedCount) booking.ApplyDecision(true);
            db.Bookings.Add(booking);
        }
        await db.SaveChangesAsync();
        var service = new BookingsService(new BookingRepository(db));

        await Assert.ThrowsAsync<ActiveBookingLimitExceededException>(() => service.CreateBookingAsync(Guid.NewGuid(), user));
        Assert.Equal(10, await db.Bookings.CountAsync());
    }

    [Fact]
    public async Task TenthBooking_IsAllowed_ExcludingInactiveAndOtherUsersBookings()
    {
        await using var db = CreateContext();
        var user = Guid.NewGuid();
        for (var i = 0; i < 9; i++) db.Bookings.Add(Booking.Create(Guid.NewGuid(), user));
        var rejected = Booking.Create(Guid.NewGuid(), user);
        rejected.ApplyDecision(false);
        var cancelled = Booking.Create(Guid.NewGuid(), user);
        cancelled.Cancel();
        db.Bookings.AddRange(rejected, cancelled, Booking.Create(Guid.NewGuid(), Guid.NewGuid()));
        await db.SaveChangesAsync();
        var repository = new BookingRepository(db);
        var service = new BookingsService(repository);

        var result = await service.CreateBookingAsync(Guid.NewGuid(), user);

        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Equal(10, await repository.CountActiveAsync(user));
        Assert.Equal(13, await db.Bookings.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelTwice_ThrowsConflictWithoutChangingFirstCancellation(bool confirmed)
    {
        await using var db = CreateContext();
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid());
        if (confirmed) booking.ApplyDecision(true);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        var service = new BookingsService(new BookingRepository(db));
        var first = await service.CancelBookingAsync(booking.Id, booking.UserId, false);

        await Assert.ThrowsAsync<ConflictException>(() => service.CancelBookingAsync(booking.Id, booking.UserId, false));

        db.ChangeTracker.Clear();
        var saved = await db.Bookings.SingleAsync();
        Assert.Equal(BookingStatus.Cancelled, saved.Status);
        Assert.Equal(first.ProcessedAt, saved.ProcessedAt);
        Assert.True(saved.DeliveryPending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherUsersBooking_OnlyAdminCanReadAndCancel(bool isAdmin)
    {
        await using var db = CreateContext();
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid());
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        var service = new BookingsService(new BookingRepository(db));
        var visitor = Guid.NewGuid();

        if (isAdmin)
        {
            Assert.Equal(booking.Id, (await service.GetBookingByIdAsync(booking.Id, visitor, true)).Id);
            Assert.Equal(BookingStatus.Cancelled, (await service.CancelBookingAsync(booking.Id, visitor, true)).Status);
        }
        else
        {
            await Assert.ThrowsAsync<InsufficientPermissionsException>(() => service.GetBookingByIdAsync(booking.Id, visitor, false));
            await Assert.ThrowsAsync<InsufficientPermissionsException>(() => service.CancelBookingAsync(booking.Id, visitor, false));
        }
        db.ChangeTracker.Clear();
        var saved = await db.Bookings.SingleAsync();
        Assert.Equal(isAdmin ? BookingStatus.Cancelled : BookingStatus.Pending, saved.Status);
        Assert.Equal(booking.UserId, saved.UserId);
    }
}
