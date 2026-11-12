using Events.Domain.Consts;


namespace Events.Domain.Models;

public class Event
{
    private readonly Lock _seatsLock = new();

    public Guid Id { get; init; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; private set; }
    public int HeldSeats { get; private set; }
    public int AvailableSeats { get; private set; }

    public Event()
    {

    }

    private Event(Guid id, string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
    {
        ValidateTitle(title);
        ValidateDates(startAt, endAt);
        ValidateTotalSeats(totalSeats);

        Id = id;
        Title = title;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
    }

    public static Event Create(
        Guid id,
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats) =>
        new(id, title, description, startAt, endAt, totalSeats);

    public static Event Create(Guid id, string title, DateTime startAt, DateTime endAt, int totalSeats) =>
        new(id, title, null, startAt, endAt, totalSeats);

    public bool TryHoldSeats(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        lock (_seatsLock)
        {
            if (AvailableSeats - HeldSeats < count) return false;
            HeldSeats += count;
            return true;
        }
    }

    public bool TryConfirmHold(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        lock (_seatsLock)
        {
            if (HeldSeats < count || AvailableSeats < count) return false;
            HeldSeats -= count;
            AvailableSeats -= count;
            return true;
        }
    }

    public void ReleaseHold(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        lock (_seatsLock) HeldSeats -= count;
    }

    public bool TryReserveSeats(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        lock (_seatsLock)
        {
            if (AvailableSeats - HeldSeats < count)
                return false;

            AvailableSeats -= count;
            return true;
        }
    }

    public void ReleaseSeats(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        lock (_seatsLock)
        {
            AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
        }
    }

    public bool TryUpdateCapacity(int totalSeats)
    {
        ValidateTotalSeats(totalSeats);

        lock (_seatsLock)
        {
            var reservedSeats = TotalSeats - AvailableSeats;
            if (totalSeats < reservedSeats + HeldSeats)
                return false;

            TotalSeats = totalSeats;
            AvailableSeats = totalSeats - reservedSeats;
            return true;
        }
    }

    private static void ValidateDates(DateTime startAt, DateTime endAt)
    {
        if (endAt <= startAt)
            throw new ArgumentException(ErrorsMessages.CannotCreateEventWithStartLaterThenEnd, nameof(endAt));
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title cannot be empty.", nameof(title));
    }

    private static void ValidateTotalSeats(int totalSeats)
    {
        if (totalSeats <= 0)
            throw new ArgumentException("totalSeats must be greater than zero.", nameof(totalSeats));
    }
}
