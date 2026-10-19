using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Events.Incoming;

public record UpdateTitleDto
{
    [Required]
    [MinLength(5)]
    public required string Title { get; init; }
}
