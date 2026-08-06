using System.ComponentModel.DataAnnotations;

namespace Events_API.DTOs.Events;

public record CreateEventDto
{
    [Required][MinLength(5)] public required string Title { get; set; }
    public string? Description { get; set; }
    [Required] public required DateTime StartAt { get; set; }
    [Required] public required DateTime EndAt { get; set; }
}