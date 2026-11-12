using Contracts;

namespace Bookings.Application.Messaging;

public interface IBookingPublisher
{
    Task RequestSeats(ReserveSeats request, CancellationToken ct);
    Task Confirm(BookingConfirmed confirmation, CancellationToken ct);
    Task Cancel(Guid eventId, BookingCancelled cancellation, CancellationToken ct);
}
