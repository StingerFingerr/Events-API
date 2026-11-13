using Bookings.Application.Messaging;
using Bookings.Domain.Models;
using Bookings.Infrastructure.DataAccess;
using Contracts;

namespace Bookings.Infrastructure.Messaging;

public sealed class ReservationDecisionHandler(BookingsDbContext db, IBookingPublisher publisher)
{
    public async Task Handle(SeatsDecided decision, CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([decision.BookingId], ct);
        if (booking is null) return;
        if (booking.Status == BookingStatus.Pending)
        {
            booking.ApplyDecision(decision.Accepted);
            await db.SaveChangesAsync(ct);
        }

        // Survives a failed publish and a concurrent cancellation. The decision offset
        // is only committed once Kafka acknowledges the confirmation.
        if (booking.UndeliveredConfirmationAt is { } confirmedAt)
        {
            await publisher.Confirm(new BookingConfirmed(booking.Id, booking.EventId, booking.UserId,
                booking.SeatCount, new DateTimeOffset(confirmedAt)), ct);
            booking.MarkConfirmationDelivered();
            await db.SaveChangesAsync(ct);
        }
    }
}
