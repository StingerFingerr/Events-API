using Events_API.DataAccess;
using Events_API.DTOs.Events.Incoming;
using Events_API.Models;
using Events_API.Repositories.Events;
using Microsoft.EntityFrameworkCore;

namespace Test;

// PostgreSQL ILIKE is covered by the integration tests. This test double lets
// service tests keep using InMemory without provider-specific SQL functions.
internal sealed class InMemoryEventRepository(AppDbContext context) : IEventRepository
{
    private readonly EventRepository _repository = new(context);

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<Event?> GetByIdWithReloadAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdWithReloadAsync(id, cancellationToken);

    public Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default) =>
        context.Events.AnyAsync(e => e.Title.Equals(title, StringComparison.OrdinalIgnoreCase), cancellationToken);

    public async Task<(List<Event> Items, int TotalItems)> GetByFiltersAsync(
        GetEventsByFiltersDto filters, CancellationToken cancellationToken = default)
    {
        var filtered = context.Events.AsNoTracking();
        if (filters.Title is not null)
            filtered = filtered.Where(e => e.Title.Contains(filters.Title, StringComparison.OrdinalIgnoreCase));
        if (filters.From is not null)
            filtered = filtered.Where(e => e.StartAt >= filters.From);
        if (filters.To is not null)
            filtered = filtered.Where(e => e.StartAt <= filters.To);

        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await filtered.OrderBy(e => e.StartAt)
            .Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync(cancellationToken);
        return (items, totalItems);
    }

    public Task AddAsync(Event eventData, CancellationToken cancellationToken = default) =>
        _repository.AddAsync(eventData, cancellationToken);

    public Task RemoveAsync(Event eventData, CancellationToken cancellationToken = default) =>
        _repository.RemoveAsync(eventData, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _repository.SaveChangesAsync(cancellationToken);
}
