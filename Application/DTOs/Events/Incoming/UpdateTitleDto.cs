using System.ComponentModel.DataAnnotations;

namespace Events_API.DTOs.Events;

public record UpdateTitleDto
{
    [Required]
    [MinLength(5)]
    public required string Title { get; init; }
}
