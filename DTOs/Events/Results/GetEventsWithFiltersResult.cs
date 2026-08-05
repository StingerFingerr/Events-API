namespace Events_API.DTOs.Events.Results;

public record GetEventsWithFiltersResult(List<EventDto> Events, int Page, int PageSize, int TotalItems, int TotalPages) :
    PaginatedResult(Page, PageSize, TotalItems, TotalPages);