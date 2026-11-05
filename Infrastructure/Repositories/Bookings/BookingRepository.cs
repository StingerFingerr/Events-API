using System.Data;
using Application.Repositories.Bookings;
using Infrastructure.DataAccess;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Repositories.Bookings;

public class BookingRepository(AppDbContext context) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Bookings.FindAsync([id], cancellationToken);

    public Task<int> CountActiveAsync(Guid userId) =>
        context.Bookings.CountAsync(b => b.UserId == userId &&
            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

    public async Task<T> RunBookingChangeAsync<T>(Func<Task<T>> change)
    {
        if (!context.Database.IsRelational())
            return await change();

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await change();
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException ||
                                   ex.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            context.ChangeTracker.Clear();
            throw new Domain.Exceptions.ConflictException("Booking changed concurrently. Retry the operation.");
        }
    }

    public Task<Guid[]> GetPendingIdsAsync(int batchSize, CancellationToken cancellationToken = default) =>
        context.Bookings.AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.CreatedAt)
            .Take(batchSize)
            .Select(booking => booking.Id)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        try
        {
            context.Bookings.Add(booking);
            await SaveChangesAsync(cancellationToken);
        }
        catch
        {
            context.Entry(booking).State = EntityState.Detached;
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}
