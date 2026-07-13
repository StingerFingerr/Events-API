using Events_API.DTOs.Events;

namespace Events_API.Models;

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public EventDto AsDto()
    {
        return new EventDto()
        {
            Id = Id,
            Title = Title,
            Description = Description,
            StartAt = StartAt,
            EndAt = EndAt,
        };
    }
}