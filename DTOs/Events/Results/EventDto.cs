namespace Events_API.DTOs.Events.Results;

public record EventDto(int Id, string Title, string? Description, DateTime StartAt, DateTime EndAt);