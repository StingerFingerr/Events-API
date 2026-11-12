using System.Net.Http.Json;
using Contracts;
using Bookings.Domain.Models;
using Bookings.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookings.Infrastructure.Messaging;

// The booking row is also the durable delivery queue. Only clear the flag after acknowledgement.
public sealed class BookingDispatcher(IServiceScopeFactory scopes, HttpClient client,
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
                using (var response = await Post(BookingTopics.ReserveSeats,
                    new ReserveSeats(id, booking.EventId, booking.SeatCount), stop))
                {
                    var decision = await response.Content.ReadFromJsonAsync<ReservationDecision>(stop);
                    booking.ApplyDecision((decision ?? throw new InvalidDataException("Missing decision")).Accepted);
                }
                break;
            case BookingStatus.Confirmed:
                using (await Post(BookingTopics.BookingConfirmed, new BookingConfirmed(id, booking.EventId,
                    booking.UserId, booking.SeatCount, new DateTimeOffset(booking.ProcessedAt!.Value)), stop))
                    booking.MarkDelivered();
                break;
            case BookingStatus.Cancelled:
                using (await Post(BookingTopics.BookingCancelled, new BookingCancelled(id), stop))
                    booking.MarkDelivered();
                break;
        }
        await db.SaveChangesAsync(stop);
    }

    private async Task<HttpResponseMessage> Post<T>(string topic, T message, CancellationToken stop)
    {
        var response = await client.PostAsJsonAsync($"internal/messages/{topic}", message, stop);
        try { return response.EnsureSuccessStatusCode(); }
        catch { response.Dispose(); throw; }
    }
}
