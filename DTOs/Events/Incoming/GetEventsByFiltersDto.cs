using System.ComponentModel.DataAnnotations;

namespace Events_API.DTOs.Events.Incoming;

public class GetEventsByFiltersDto
{
    public string? Title { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [Range(1, 100)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}