using Events_API.Consts;
using Events_API.DTOs.Events.Results;

namespace Events_API.Models;

public class Event
{
    public int Id { get; init; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public Event(int id, string title, string? description, DateTime startAt, DateTime endAt)
    {
        ValidateTitle(title);
        ValidateDates(startAt, endAt);
        
        Id = id;
        Title = title;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
    }

    public Event(int id, string title, DateTime startAt, DateTime endAt)
    {
        ValidateTitle(title);
        ValidateDates(startAt, endAt);
        
        Id = id;
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        Description = null;
    }

    public EventDto AsDto() =>
        new(Id, Title, Description, StartAt, EndAt);

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
}