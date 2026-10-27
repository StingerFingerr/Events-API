using Application.Repositories.Events;
using Infrastructure.DataAccess;
using Application.DTOs.Events.Incoming;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Events;

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
        // Reload detaches the entity if another context deleted its database row.
        return context.Entry(eventData).State == EntityState.Detached ? null : eventData;
    }

    public Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        var pattern = EscapeLikePattern(title);
        return context.Events.AnyAsync(e => EF.Functions.ILike(e.Title, pattern, "\\"), cancellationToken);
    }

    public async Task<(List<Event> Items, int TotalItems)> GetByFiltersAsync(
        GetEventsByFiltersDto filters, CancellationToken cancellationToken = default)
    {
        var filtered = context.Events.AsNoTracking();

        if (filters.Title is not null)
        {
            var pattern = $"%{EscapeLikePattern(filters.Title)}%";
            filtered = filtered.Where(e => EF.Functions.ILike(e.Title, pattern, "\\"));
        }
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

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
