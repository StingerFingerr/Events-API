namespace Events_API.DTOs.Events;

public record UpdateTitleDto
{
    public required string Title { get; init; }
}