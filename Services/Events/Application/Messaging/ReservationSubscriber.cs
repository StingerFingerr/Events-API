using Contracts;
using Microsoft.Extensions.Logging;
using Events.Domain.Models;
using Events.Application.Repositories.Events;
using Events.Application.Caching;

namespace Events.Application.Messaging;

public sealed class ReservationSubscriber(IReservationRepository repository, ILogger<ReservationSubscriber> logger,
    ICacheService cache)
{
    public async Task<ReservationDecision> Reserve(ReserveSeats message, CancellationToken ct)
    {
        var receipt = await repository.GetReceipt(message.BookingId, ct);
        if (receipt != null)
            return new(receipt.State is ReservationState.Held or ReservationState.Confirmed);

        var item = await repository.GetEvent(message.EventId, ct);
        var accepted = item != null && item.StartAt > DateTime.UtcNow && item.TryHoldSeats(message.SeatCount);
        repository.Add(new()
        {
            Id = message.BookingId,
            EventId = message.EventId,
            SeatCount = message.SeatCount,
            State = accepted ? ReservationState.Held : ReservationState.Rejected
        });
        await repository.Save(ct);
        if (accepted)
            await cache.RemoveAsync($"event:{message.EventId}");
        return new(accepted);
    }

    public async Task Confirm(BookingConfirmed message, CancellationToken ct)
    {
        var receipt = await repository.GetReceipt(message.BookingId, ct);
        if (receipt != null && receipt.State != ReservationState.Held) return;
        if (receipt != null && (receipt.EventId != message.EventId || receipt.SeatCount != message.SeatCount))
        {
            logger.LogWarning("Skipping confirmation {BookingId}: it does not match the hold", message.BookingId);
            return;
        }

        var item = await repository.GetEvent(message.EventId, ct);
        var accepted = item != null && (receipt != null
            ? item.TryConfirmHold(message.SeatCount)
            : item.TryReserveSeats(message.SeatCount));
        if (!accepted)
        {
            logger.LogWarning("Skipping confirmation {BookingId} for event {EventId}: {Reason}", message.BookingId,
                message.EventId, item == null ? "event not found" : "insufficient seats");
        }

        var state = accepted ? ReservationState.Confirmed : ReservationState.Rejected;
        if (receipt == null)
            repository.Add(new() { Id = message.BookingId, EventId = message.EventId, SeatCount = message.SeatCount, State = state });
        else
            receipt.State = state;
        await repository.Save(ct);
        if (accepted)
            await cache.RemoveAsync($"event:{message.EventId}");
    }

    public async Task Cancel(BookingCancelled message, CancellationToken ct)
    {
        Guid? changedEventId = null;
        var receipt = await repository.GetReceipt(message.BookingId, ct);
        if (receipt == null)
            repository.Add(new() { Id = message.BookingId, State = ReservationState.Cancelled });
        else
        {
            if (receipt.State == ReservationState.Cancelled) return;
            if (receipt.State is ReservationState.Held or ReservationState.Confirmed)
            {
                var item = await repository.GetEvent(receipt.EventId!.Value, ct);
                if (receipt.State == ReservationState.Held) item?.ReleaseHold(receipt.SeatCount);
                else item?.ReleaseSeats(receipt.SeatCount);
                changedEventId = item?.Id;
            }
            receipt.State = ReservationState.Cancelled;
        }
        await repository.Save(ct);
        if (changedEventId is { } eventId)
            await cache.RemoveAsync($"event:{eventId}");
    }
}
