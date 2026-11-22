using Contracts;
using Events.Application.Messaging;
using Events.Domain.Models;
using Events.Infrastructure.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Integration_tests.Events;

[Collection(PostgreSqlCollection.Name)]
public class ConcurrentReservationTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParallelRequestsForLastSeat_ExactlyOneSucceeds(bool directConfirmation)
    {
        var item = Event.Create(Guid.NewGuid(), "Last seat", StartAt, StartAt.AddHours(1), 1);
        await using (var setup = Database.CreateContext())
        {
            setup.Events.Add(item);
            await setup.SaveChangesAsync();
        }
        const int contenders = 5;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;
        var conflicts = 0;
        var ids = Enumerable.Range(0, contenders).Select(_ => Guid.NewGuid()).ToArray();

        async Task<bool> Attempt(Guid bookingId, bool synchronize)
        {
            await using var db = Database.CreateContext();
            if (synchronize)
            {
                // Separate contexts must all see the last seat before any writer saves.
                var snapshot = await db.Events.FindAsync(item.Id);
                Assert.Equal(1, snapshot!.AvailableSeats);
                Assert.Equal(0, snapshot.HeldSeats);
                if (Interlocked.Increment(ref readers) == contenders) ready.SetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            var handler = new ReservationSubscriber(new ReservationRepository(db), NullLogger<ReservationSubscriber>.Instance, new NullCacheService());
            try
            {
                if (directConfirmation)
                {
                    await handler.Confirm(new BookingConfirmed(bookingId, item.Id, Guid.NewGuid(), 1, DateTimeOffset.UtcNow), default);
                    return (await db.Reservations.FindAsync(bookingId))!.State == ReservationState.Confirmed;
                }
                return (await handler.Reserve(new ReserveSeats(bookingId, item.Id, 1), default)).Accepted;
            }
            catch (DbUpdateConcurrencyException) when (synchronize)
            {
                Interlocked.Increment(ref conflicts);
                return false;
            }
        }

        var results = await Task.WhenAll(ids.Select(id => Attempt(id, true)));
        Assert.Single(results, accepted => accepted);
        Assert.Equal(contenders - 1, conflicts);
        await using (var verification = Database.CreateContext())
        {
            // A failed optimistic update must also roll back its reservation receipt.
            Assert.Equal(1, await verification.Reservations.CountAsync());
        }
        for (var i = 0; i < ids.Length; i++)
            if (!results[i]) Assert.False(await Attempt(ids[i], false));

        await using var final = Database.CreateContext();
        var saved = await final.Events.SingleAsync();
        Assert.Equal(directConfirmation ? 0 : 1, saved.AvailableSeats);
        Assert.Equal(directConfirmation ? 0 : 1, saved.HeldSeats);
        Assert.Equal(0, saved.AvailableSeats - saved.HeldSeats);
        Assert.Equal(1, await final.Reservations.CountAsync(r => r.State == ReservationState.Held || r.State == ReservationState.Confirmed));
        Assert.Equal(contenders - 1, await final.Reservations.CountAsync(r => r.State == ReservationState.Rejected));
    }
}
