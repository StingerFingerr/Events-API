using Bookings.Domain.Exceptions;

namespace Bookings.Domain.Models;

public enum BookingStatus { Pending, Confirmed, Rejected, Cancelled }

public sealed class Booking
{
    public Guid Id { get; private init; }
    public Guid EventId { get; private init; }
    public Guid UserId { get; private init; }
    public int SeatCount { get; private init; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? ProcessedAt { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTime? UndeliveredConfirmationAt { get; private set; }
    public bool DeliveryPending { get; private set; }

    public static Booking Create(Guid eventId, Guid userId)
    {
        if (eventId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Event and user identifiers are required.");
        return new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            SeatCount = 1,
            CreatedAt = DateTime.UtcNow,
            DeliveryPending = true
        };
    }

    public void ApplyDecision(bool accepted)
    {
        if (Status != BookingStatus.Pending) return;
        Status = accepted ? BookingStatus.Confirmed : BookingStatus.Rejected;
        // Use PostgreSQL timestamp precision so retries serialize the same instant.
        var now = DateTime.UtcNow;
        ProcessedAt = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        UndeliveredConfirmationAt = accepted ? ProcessedAt : null;
        DeliveryPending = false;
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled) throw new ConflictException("Booking is already cancelled.");
        Status = BookingStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
        DeliveryPending = true;
    }

    public void MarkConfirmationDelivered() => UndeliveredConfirmationAt = null;

    public void MarkDelivered() => DeliveryPending = false;
}
