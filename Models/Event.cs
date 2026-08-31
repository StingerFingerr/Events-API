using Events_API.Consts;
using Events_API.DTOs.Events.Results;

namespace Events_API.Models;

public class Event
{
    private readonly Lock _seatsLock = new();

    public Guid Id { get; init; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; private set; }
    public int AvailableSeats { get; private set; }

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

    public bool TryReserveSeats(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        lock (_seatsLock)
        {
            if (AvailableSeats < count)
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
            if (totalSeats < reservedSeats)
                return false;

            TotalSeats = totalSeats;
            AvailableSeats = totalSeats - reservedSeats;
            return true;
        }
    }

    public EventDto AsDto() =>
        new(Id, Title, Description, StartAt, EndAt, TotalSeats, AvailableSeats);

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
