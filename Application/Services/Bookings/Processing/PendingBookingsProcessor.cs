using Application.Repositories.Bookings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Domain.Models;

namespace Application.Services.Bookings.Processing;

public class PendingBookingsProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<PendingBookingsProcessor> logger) : IPendingBookingsProcessor
{
    private const int BatchSize = 100;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        Guid[] bookingIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            bookingIds = await repository.GetPendingIdsAsync(BatchSize, cancellationToken);
        }
        await Task.WhenAll(bookingIds.Select(id => ProcessBookingAsync(id, cancellationToken)));
        return bookingIds.Length;
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing booking {BookingId}", bookingId);
            // Simulate the external confirmation call.
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var booking = await repository.GetByIdAsync(bookingId, cancellationToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
                return;

            booking.Confirm();
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Booking {BookingId} confirmed", bookingId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Booking {BookingId} processing failed; it remains pending for retry", bookingId);
        }
    }
}
