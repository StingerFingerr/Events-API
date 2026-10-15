using Events_API.DataAccess;
using Events_API.DTOs.Events.Incoming;
using Events_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Events_API.Repositories.Events;

public class EventRepository(AppDbContext context) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Events.FindAsync([id], cancellationToken);

    public async Task<Event?> GetByIdWithReloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var eventData = await GetByIdAsync(id, cancellationToken);
        if (eventData is null)
            return null;

        await context.Entry(eventData).ReloadAsync(cancellationToken);
        return context.Entry(eventData).State == EntityState.Detached ? null : eventData;
    }

    public Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default) =>
        context.Events.AnyAsync(e => e.Title.ToLower() == title.ToLower(), cancellationToken);

    public async Task<(List<Event> Items, int TotalItems)> GetByFiltersAsync(
        GetEventsByFiltersDto filters, CancellationToken cancellationToken = default)
    {
        var filtered = context.Events.AsNoTracking();

        if (filters.Title is not null)
            filtered = filtered.Where(e => e.Title.ToLower().Contains(filters.Title.ToLower()));
        if (filters.From is not null)
            filtered = filtered.Where(e => e.StartAt >= filters.From);
        if (filters.To is not null)
            filtered = filtered.Where(e => e.StartAt <= filters.To);

        var items = await filtered
            .OrderBy(e => e.StartAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);
        var totalItems = await filtered.CountAsync(cancellationToken);
        return (items, totalItems);
    }

    public async Task AddAsync(Event eventData, CancellationToken cancellationToken = default)
    {
        context.Events.Add(eventData);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Event eventData, CancellationToken cancellationToken = default)
    {
        context.Events.Remove(eventData);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}
