using Events_API.Consts;
using Events_API.DTOs.Events.Results;

namespace Events_API.Models;

public class Event
{
    public Guid Id { get; init; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }

    public Event(Guid id, string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
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

    public Event(Guid id, string title, DateTime startAt, DateTime endAt, int totalSeats)
    {
        ValidateTitle(title);
        ValidateDates(startAt, endAt);
        ValidateTotalSeats(totalSeats);

        Id = id;
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        Description = null;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
    }

    public bool TryReserveSeats(int count = 1)
    {
        var canReserve = AvailableSeats - count >= 0;
        if (canReserve)
            AvailableSeats -=  count;
        return canReserve;
    }

    public void ReleaseSeats(int count = 1)
    {
        AvailableSeats += count;
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
