using Events_API.DataAccess;
using Events_API.Exceptions;
using Events_API.Models;
using Events_API.Services.Bookings;
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
            try { return (await service.CreateBookingAsync(eventData.Id)).Id; }
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
        var booking = await service.CreateBookingAsync(eventData.Id);
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.InRange(booking.CreatedAt, before, DateTime.UtcNow);
        using var otherScope = _serviceProvider.CreateScope();
        var otherService = otherScope.ServiceProvider.GetRequiredService<IBookingService>();
        await otherService.UpdateBookingStatusAsync(booking.Id, status);
        context.ChangeTracker.Clear();
        var saved = await service.GetBookingByIdAsync(booking.Id);
        Assert.Equal(status, saved.Status);
        Assert.Equal(status == BookingStatus.Pending, saved.ProcessedAt is null);
        Assert.Equal(1, (await context.Events.SingleAsync()).AvailableSeats);
    }

    [Fact]
    public async Task MissingEntities_ThrowNotFound()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBookingAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBookingByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateBookingStatusAsync(Guid.NewGuid(), BookingStatus.Confirmed));
    }
}
