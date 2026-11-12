using Contracts;
using Events.Domain.Models;
using Events.Infrastructure.DataAccess;
using Events.Application.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Test;

public class MessageDeliveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReplayedMessagesCannotConsumeOrReleaseAnExtraSeat(bool cancelFirst)
    {
        using var db = new EventsDbContext(new DbContextOptionsBuilder<EventsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var start = DateTime.UtcNow.AddDays(1);
        var item = Event.Create(Guid.NewGuid(), "Concert", start, start.AddHours(1), 3);
        db.Events.Add(item);
        await db.SaveChangesAsync();
        var handler = new ReservationSubscriber(new Events.Infrastructure.Repositories.Events.ReservationRepository(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ReservationSubscriber>.Instance);
        var request = new ReserveSeats(Guid.NewGuid(), item.Id, 2);
        var cancel = new BookingCancelled(request.BookingId);
        var confirmation = new BookingConfirmed(request.BookingId, item.Id, Guid.NewGuid(), 2, DateTimeOffset.UtcNow);
        if (cancelFirst) await handler.Cancel(cancel, default);
        Assert.Equal(!cancelFirst, (await handler.Reserve(request, default)).Accepted);
        Assert.Equal(3, item.AvailableSeats);
        Assert.Equal(cancelFirst ? 0 : 2, item.HeldSeats);
        Assert.False((await handler.Reserve(new ReserveSeats(Guid.NewGuid(), item.Id, cancelFirst ? 4 : 2), default)).Accepted);
        foreach (var attempt in Enumerable.Range(0, 2))
        {
            Assert.Equal(!cancelFirst, (await handler.Reserve(request, default)).Accepted);
            await handler.Confirm(confirmation, default);
        }
        Assert.Equal(cancelFirst ? 3 : 1, item.AvailableSeats);
        await handler.Cancel(cancel, default);
        await handler.Cancel(cancel, default);
        Assert.False((await handler.Reserve(request, default)).Accepted);
        Assert.Equal(3, item.AvailableSeats);
    }
}
