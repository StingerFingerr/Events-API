namespace Contracts;

public static class BookingTopics
{
    public const string SeatsDecided = "seats-decided";
    public const string ReserveSeats = "reserve-seats";
    public const string BookingConfirmed = "booking-confirmed";
    public const string BookingCancelled = "booking-cancelled";
}

public sealed record ReserveSeats(Guid BookingId, Guid EventId, int SeatCount);
public sealed record ReservationDecision(bool Accepted);
public sealed record BookingConfirmed(Guid BookingId, Guid EventId, Guid UserId, int SeatCount, DateTimeOffset ConfirmedAt);
public sealed record BookingCancelled(Guid BookingId);

public sealed record SeatsDecided(Guid BookingId, bool Accepted);
