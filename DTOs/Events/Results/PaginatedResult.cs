namespace Events_API.DTOs.Events.Results;

public record PaginatedResult(int Page, int PageSize, int TotalItems, int TotalPages);