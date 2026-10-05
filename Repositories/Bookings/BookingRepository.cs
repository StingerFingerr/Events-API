using Events_API.DataAccess;
using Events_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Events_API.Repositories.Bookings;

public class BookingRepository(AppDbContext context) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Bookings.FindAsync([id], cancellationToken);

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
