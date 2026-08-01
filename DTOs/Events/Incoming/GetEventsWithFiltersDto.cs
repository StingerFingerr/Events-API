namespace Events_API.DTOs.Events;

public class GetEventsWithFiltersDto
{
    public string? Title { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}