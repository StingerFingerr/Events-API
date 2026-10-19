namespace Application.DTOs.Events.Results;

public record PaginatedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);