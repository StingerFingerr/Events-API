using Events_API.DataAccess;
using Events_API.Models;
using Events_API.Services.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Events_API.Background_tasks.Booking;

public class BookingBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ExternalCallDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Guid[] bookingIds;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    bookingIds = await context.Bookings.AsNoTracking()
                        .Where(booking => booking.Status == BookingStatus.Pending)
                        .OrderBy(booking => booking.CreatedAt)
                        .Take(BatchSize)
                        .Select(booking => booking.Id)
                        .ToArrayAsync(stoppingToken);
                }

                if (bookingIds.Length == 0)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                    continue;
                }

                await Task.WhenAll(bookingIds.Select(id => ProcessBookingAsync(id, stoppingToken)));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Processing booking {BookingId}", bookingId);
            await Task.Delay(ExternalCallDelay, stoppingToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await context.Bookings.FindAsync([bookingId], stoppingToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
                return;
            var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
            await service.UpdateBookingStatusAsync(bookingId, BookingStatus.Confirmed);
            logger.LogInformation("Booking {BookingId} confirmed", bookingId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Booking {BookingId} processing failed; it remains pending for retry", bookingId);
        }
    }
}
