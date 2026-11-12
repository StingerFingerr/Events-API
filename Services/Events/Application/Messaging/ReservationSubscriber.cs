using Contracts;
using Events.Domain.Models;
using Events.Application.Repositories.Events;

namespace Events.Application.Messaging;

public sealed class ReservationSubscriber(IReservationRepository repository)
{
    public async Task<ReservationDecision> Reserve(ReserveSeats message, CancellationToken ct)
    {
        var receipt = await repository.GetReceipt(message.BookingId, ct);
        if (receipt != null)
            return new(receipt.State is ReservationState.Held or ReservationState.Confirmed);

        var item = await repository.GetEvent(message.EventId, ct);
        var accepted = item != null && item.StartAt > DateTime.UtcNow && item.TryReserveSeats(message.SeatCount);
        repository.Add(new() { Id = message.BookingId, EventId = message.EventId,
            SeatCount = message.SeatCount, State = accepted ? ReservationState.Held : ReservationState.Rejected });
        await repository.Save(ct);
        return new(accepted);
    }

    public async Task Confirm(BookingConfirmed message, CancellationToken ct)
    {
        var receipt = await repository.GetReceipt(message.BookingId, ct);
        if (receipt is null) throw new InvalidOperationException("Missing seat reservation.");
        if (receipt.State != ReservationState.Held) return;
        if (receipt.EventId != message.EventId || receipt.SeatCount != message.SeatCount)
            throw new InvalidOperationException("Reservation does not match confirmation.");
        receipt.State = ReservationState.Confirmed;
        await repository.Save(ct);
    }

    public async Task Cancel(BookingCancelled message, CancellationToken ct)
    {
        var receipt = await repository.GetReceipt(message.BookingId, ct);
        if (receipt == null)
            repository.Add(new() { Id = message.BookingId, State = ReservationState.Cancelled });
        else
        {
            if (receipt.State == ReservationState.Cancelled) return;
            if (receipt.State is ReservationState.Held or ReservationState.Confirmed)
            {
                var item = await repository.GetEvent(receipt.EventId!.Value, ct);
                item?.ReleaseSeats(receipt.SeatCount);
            }
            receipt.State = ReservationState.Cancelled;
        }
        await repository.Save(ct);
    }
}
