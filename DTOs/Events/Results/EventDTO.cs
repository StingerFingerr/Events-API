namespace Events_API.DTOs.Events;

public record EventDto(int Id, string Title, string? Description, DateTime StartAt, DateTime EndAt);