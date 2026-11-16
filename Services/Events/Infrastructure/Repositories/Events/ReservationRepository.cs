using Events.Application.Repositories.Events;
using Events.Domain.Models;
using Events.Infrastructure.DataAccess;

namespace Events.Infrastructure.Repositories.Events;

public sealed class ReservationRepository(EventsDbContext db) : IReservationRepository
{
    public ValueTask<SeatReservation?> GetReceipt(Guid bookingId, CancellationToken ct) =>
        db.Reservations.FindAsync([bookingId], ct);
    public ValueTask<Event?> GetEvent(Guid eventId, CancellationToken ct) =>
        db.Events.FindAsync([eventId], ct);
    public void Add(SeatReservation receipt) => db.Reservations.Add(receipt);
    public Task Save(CancellationToken ct) => db.SaveChangesAsync(ct);
}
