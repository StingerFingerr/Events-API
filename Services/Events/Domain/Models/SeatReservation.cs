namespace Events.Domain.Models;

public enum ReservationState { Held, Confirmed, Rejected, Cancelled }

// Local receipt, keyed by the caller's booking ID. A cancellation can arrive before a hold.
public sealed class SeatReservation
{
    public Guid Id { get; init; }
    public Guid? EventId { get; init; }
    public int SeatCount { get; init; }
    public ReservationState State { get; set; }
}
