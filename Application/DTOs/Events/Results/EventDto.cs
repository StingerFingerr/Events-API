namespace Application.DTOs.Events.Results;

public record EventDto(Guid Id, string Title, string? Description, DateTime StartAt, DateTime EndAt,
    int TotalSeats, int AvailableSeats);
