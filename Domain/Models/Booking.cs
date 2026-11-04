using Domain.Exceptions;

namespace Domain.Models;

public class Booking
{
    private readonly Lock _stateLock = new();
    private bool _seatsReleased;

    public Guid Id { get; init; }
    public Event Event { get; init; } = null!;
    public Guid EventId { get; init; }
    public Guid? UserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public BookingStatus Status { get; set; }

    internal Event? ReservedEvent { get; init; }

    public Booking()
    {

    }

    public static Booking Create(Event eventData, Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        var now = DateTime.UtcNow;
        if (eventData.StartAt <= now)
            throw new PastEventBookingException();
        return new Booking
        {
            Id = Guid.NewGuid(),
            Event = eventData,
            EventId = eventData.Id,
            UserId = userId,
            CreatedAt = now,
            Status = BookingStatus.Pending
        };
    }

    public void Cancel()
    {
        lock (_stateLock)
        {
            if (Status == BookingStatus.Cancelled)
                throw new ConflictException("Booking is already cancelled.");
            Status = BookingStatus.Cancelled;
            ProcessedAt = DateTime.UtcNow;
        }
    }

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
    Rejected,
    Cancelled
}
