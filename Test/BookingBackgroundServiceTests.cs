using Infrastructure.Repositories.Bookings;
using Infrastructure.Repositories.Events;
using Infrastructure.BackgroundTasks.Booking;
using Infrastructure.DataAccess;
using Application.Repositories.Events;
using Application.Repositories.Bookings;
using Domain.Models;
using Application.Services.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Application;

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
        services.AddApplication();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventData = Event.Create(Guid.NewGuid(), "concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 3);
        context.Events.Add(eventData);
        await context.SaveChangesAsync();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        for (var i = 0; i < 3; i++)
            await bookingService.CreateBookingAsync(eventData.Id);
        using var worker = new BookingBackgroundService(provider.GetRequiredService<IServiceScopeFactory>());
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (await context.Bookings.AsNoTracking().AnyAsync(b => b.Status == BookingStatus.Pending, timeout.Token))
                await Task.Delay(20, timeout.Token);
            var saved = await context.Bookings.AsNoTracking().ToArrayAsync();
            Assert.Equal(3, saved.Length);
            Assert.All(saved, booking =>
            {
                Assert.Equal(BookingStatus.Confirmed, booking.Status);
                Assert.NotNull(booking.ProcessedAt);
            });
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }
}
