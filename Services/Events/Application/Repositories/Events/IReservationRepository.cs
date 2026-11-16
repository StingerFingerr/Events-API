using Events.Domain.Models;

namespace Events.Application.Repositories.Events;

public interface IReservationRepository
{
    ValueTask<SeatReservation?> GetReceipt(Guid bookingId, CancellationToken ct);
    ValueTask<Event?> GetEvent(Guid eventId, CancellationToken ct);
    void Add(SeatReservation receipt);
    Task Save(CancellationToken ct);
}
