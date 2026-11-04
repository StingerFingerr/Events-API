using Domain.Models;

namespace Application.Repositories.Bookings;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(Guid userId);
    Task<T> RunBookingChangeAsync<T>(Func<Task<T>> change);
    Task<Guid[]> GetPendingIdsAsync(int batchSize, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
