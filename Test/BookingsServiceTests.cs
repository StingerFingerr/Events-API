using Infrastructure.DataAccess;
using Domain.Exceptions;
using Domain.Models;
using Application.Services.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public class BookingsServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider = TestDatabase.CreateServiceProvider();

    public void Dispose() => _serviceProvider.Dispose();

    [Theory]
    [InlineData(10, 10)]
    [InlineData(5, 20)]
    public async Task ConcurrentScopes_PreventOverbooking(int seats, int requests)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventData = Event.Create(Guid.NewGuid(), "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), seats);
        context.Events.Add(eventData);
        await context.SaveChangesAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, requests).Select(_ => Task.Run(async () =>
        {
            using var requestScope = _serviceProvider.CreateScope();
            var scopeContext = requestScope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Exercise contexts that have already tracked the event before acquiring the semaphore.
            await scopeContext.Events.FindAsync(eventData.Id);
            var service = requestScope.ServiceProvider.GetRequiredService<IBookingService>();
            try { return (await service.CreateBookingAsync(eventData.Id, Guid.NewGuid())).Id; }
            catch (NoAvailableSeatsException) { return Guid.Empty; }
        })));
        context.ChangeTracker.Clear();
        Assert.Equal(seats, results.Count(id => id != Guid.Empty));
        Assert.Equal(seats, results.Where(id => id != Guid.Empty).Distinct().Count());
        Assert.Equal(seats, await context.Bookings.CountAsync());
        Assert.Equal(0, (await context.Events.SingleAsync()).AvailableSeats);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Pending)]
    public async Task CreateAndUpdate_PersistAcrossContexts(BookingStatus status)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventData = Event.Create(Guid.NewGuid(), "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 2);
        context.Events.Add(eventData);
        await context.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var before = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var booking = await service.CreateBookingAsync(eventData.Id, userId);
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, before, DateTime.UtcNow);
        using var otherScope = _serviceProvider.CreateScope();
        var otherService = otherScope.ServiceProvider.GetRequiredService<IBookingService>();
        await otherService.UpdateBookingStatusAsync(booking.Id, status);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ProcessedAt);
        context.ChangeTracker.Clear();
        var saved = await service.GetBookingByIdAsync(booking.Id, userId, UserRole.User);
        Assert.Equal(status, saved.Status);
        Assert.Equal(status == BookingStatus.Pending, saved.ProcessedAt is null);
        Assert.Equal(1, (await context.Events.SingleAsync()).AvailableSeats);
    }

    [Fact]
    public async Task CreateBooking_PastEvent_DoesNotSaveBookingOrConsumeSeat()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var concert = await AddConcertAsync(context, DateTime.UtcNow.AddDays(-1), 1);

        await Assert.ThrowsAsync<PastEventBookingException>(
            () => service.CreateBookingAsync(concert.Id, Guid.NewGuid()));

        context.ChangeTracker.Clear();
        Assert.False(await context.Bookings.AnyAsync());
        Assert.Equal(1, (await context.Events.SingleAsync()).AvailableSeats);
    }

    [Fact]
    public async Task CreateBooking_TenActiveBookings_PreventBookingAnotherEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var start = DateTime.UtcNow.AddDays(1);
        var concert = await AddConcertAsync(context, start, 10);
        var nextConcert = await AddConcertAsync(context, start.AddDays(1), 1);
        var userId = Guid.NewGuid();
        var first = await service.CreateBookingAsync(concert.Id, userId);
        await service.UpdateBookingStatusAsync(first.Id, BookingStatus.Confirmed);
        for (var i = 1; i < 10; i++)
            await service.CreateBookingAsync(concert.Id, userId);

        await Assert.ThrowsAsync<ActiveBookingLimitExceededException>(
            () => service.CreateBookingAsync(nextConcert.Id, userId));

        context.ChangeTracker.Clear();
        Assert.Equal(10, await context.Bookings.CountAsync(b => b.UserId == userId));
        Assert.False(await context.Bookings.AnyAsync(b => b.EventId == nextConcert.Id));
        Assert.Equal(1, (await context.Events.SingleAsync(e => e.Id == nextConcert.Id)).AvailableSeats);
    }

    [Fact]
    public async Task CreateBooking_TwoUsers_CanEachReserveTenSeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var concert = await AddConcertAsync(context, DateTime.UtcNow.AddDays(1), 21);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        foreach (var userId in new[] { firstUser, secondUser })
        {
            for (var i = 0; i < 10; i++)
                await service.CreateBookingAsync(concert.Id, userId);
        }

        Assert.Equal(10, await context.Bookings.CountAsync(b => b.UserId == firstUser));
        Assert.Equal(10, await context.Bookings.CountAsync(b => b.UserId == secondUser));
    }

    private static async Task<Event> AddConcertAsync(AppDbContext context, DateTime start, int seats)
    {
        var concert = Event.Create(Guid.NewGuid(), "concert", start, start.AddHours(2), seats);
        context.Events.Add(concert);
        await context.SaveChangesAsync();
        return concert;
    }

    [Fact]
    public async Task ActiveLimitAndCancellation_RespectOwnerAndAdmin()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(1);
        var concert = Event.Create(Guid.NewGuid(), "concert", start, start.AddHours(2), 12);
        context.Events.Add(concert);
        await context.SaveChangesAsync();

        var first = await service.CreateBookingAsync(concert.Id, owner);
        await service.UpdateBookingStatusAsync(first.Id, BookingStatus.Confirmed);
        for (var i = 1; i < 10; i++)
            await service.CreateBookingAsync(concert.Id, owner);
        await Assert.ThrowsAsync<ActiveBookingLimitExceededException>(() => service.CreateBookingAsync(concert.Id, owner));
        await Assert.ThrowsAsync<InsufficientPermissionsException>(() => service.GetBookingByIdAsync(first.Id, other, UserRole.User));
        Assert.Equal(first.Id, (await service.GetBookingByIdAsync(first.Id, other, UserRole.Admin)).Id);
        var separate = await service.CreateBookingAsync(concert.Id, other);
        await Assert.ThrowsAsync<InsufficientPermissionsException>(() => service.CancelBookingAsync(first.Id, other, UserRole.User));
        await service.CancelBookingAsync(first.Id, owner, UserRole.User);
        await Assert.ThrowsAsync<ConflictException>(() => service.CancelBookingAsync(first.Id, owner, UserRole.User));
        await service.CreateBookingAsync(concert.Id, owner);
        await service.CancelBookingAsync(separate.Id, Guid.NewGuid(), UserRole.Admin);
        Assert.Equal(2, concert.AvailableSeats);
    }

    [Fact]
    public async Task MissingEntities_ThrowNotFound()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBookingByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.User));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateBookingStatusAsync(Guid.NewGuid(), BookingStatus.Confirmed));
    }
}
