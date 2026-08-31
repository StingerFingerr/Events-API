namespace Events_API.Models;

public class Booking
{
    private readonly Lock _stateLock = new();
    private bool _seatsReleased;

    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public BookingStatus Status { get; set; }

    internal Event? ReservedEvent { get; init; }

    public void Confirm()
    {
        lock (_stateLock)
        {
            Status = BookingStatus.Confirmed;
            ProcessedAt = DateTime.UtcNow;
        }
    }

    public void Reject()
    {
        lock (_stateLock)
        {
            Status = BookingStatus.Rejected;
            ProcessedAt = DateTime.UtcNow;
        }
    }

    internal void ReleaseReservedSeat(Event? eventData = null)
    {
        lock (_stateLock)
        {
            if (_seatsReleased)
                return;

            var reservedEvent = ReservedEvent ?? eventData;
            if (reservedEvent is null)
                return;

            reservedEvent.ReleaseSeats();
            _seatsReleased = true;
        }
    }
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Rejected
}
