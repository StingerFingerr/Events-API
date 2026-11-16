using Bookings.Application.Messaging;
using Contracts;
using Bookings.Domain.Models;
using Bookings.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookings.Infrastructure.Messaging;

// DeliveryPending queues seat requests and cancellations; confirmations are retried by ReservationDecisionHandler.
// Only clear the flag after acknowledgement.
public sealed class BookingDispatcher(IServiceScopeFactory scopes, IBookingPublisher publisher,
    ILogger<BookingDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
                var ids = await db.Bookings.Where(b => b.DeliveryPending)
                    .OrderBy(b => b.CreatedAt).Select(b => b.Id).ToListAsync(stop);
                foreach (var id in ids)
                {
                    try { await DispatchAsync(db, id, stop); }
                    catch (Exception e) when (!stop.IsCancellationRequested)
                    { logger.LogWarning(e, "Will retry booking {BookingId}", id); }
                    finally { db.ChangeTracker.Clear(); }
                }
            }
            catch (Exception e) when (!stop.IsCancellationRequested)
            { logger.LogWarning(e, "Cannot read pending deliveries"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stop);
        }
    }

    private async Task DispatchAsync(BookingsDbContext db, Guid id, CancellationToken stop)
    {
        var booking = await db.Bookings.FindAsync([id], stop);
        if (booking is not { DeliveryPending: true }) return;
        switch (booking.Status)
        {
            case BookingStatus.Pending:
                await publisher.RequestSeats(new ReserveSeats(id, booking.EventId, booking.SeatCount), stop);
                booking.MarkDelivered();
                break;
            case BookingStatus.Cancelled:
                await publisher.Cancel(booking.EventId, new BookingCancelled(id), stop);
                booking.MarkDelivered();
                break;
        }
        await db.SaveChangesAsync(stop);
    }
}
