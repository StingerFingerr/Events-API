using Events_API.DTOs.Events.Incoming;
using Events_API.Models;

namespace Events_API.Repositories.Events;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Event?> GetByIdWithReloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default);
    Task<(List<Event> Items, int TotalItems)> GetByFiltersAsync(GetEventsByFiltersDto filters, CancellationToken cancellationToken = default);
    Task AddAsync(Event eventData, CancellationToken cancellationToken = default);
    Task RemoveAsync(Event eventData, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
