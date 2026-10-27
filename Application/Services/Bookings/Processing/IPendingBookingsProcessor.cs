namespace Application.Services.Bookings.Processing;
public interface IPendingBookingsProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}
