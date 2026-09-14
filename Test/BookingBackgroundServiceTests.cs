using Events_API.Background_tasks.Booking;
using Events_API.DataAccess;
using Events_API.Models;
using Events_API.Services.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Test;

public class BookingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ConfirmsPendingBookingInSeparateScope()
    {
        var database = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddScoped<IBookingService, BookingsService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventData = Event.Create(Guid.NewGuid(), "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 2);
        context.Events.Add(eventData);
        await context.SaveChangesAsync();
        var booking = await scope.ServiceProvider.GetRequiredService<IBookingService>().CreateBookingAsync(eventData.Id);
        using var worker = new BookingBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BookingBackgroundService>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (await context.Bookings.AsNoTracking().AnyAsync(b => b.Id == booking.Id && b.Status == BookingStatus.Pending, timeout.Token))
                await Task.Delay(20, timeout.Token);
            var saved = await context.Bookings.AsNoTracking().SingleAsync();
            Assert.Equal(BookingStatus.Confirmed, saved.Status);
            Assert.NotNull(saved.ProcessedAt);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }
}
