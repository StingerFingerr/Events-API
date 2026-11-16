using Contracts;
using Events.Application.Messaging;
using Events.Infrastructure.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Integration_tests.Events;

[Collection(PostgreSqlCollection.Name)]
public class HeldSeatsMigrationTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task UpgradeAndConfirmPreviouslyDeductedHold_DeductsOnlyOnce()
    {
        var booking = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using var db = Database.CreateContext();
        await db.Database.MigrateAsync("20261112142856_InitialCreate");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO events (id, title, start_at, end_at, total_seats, available_seats)
            VALUES ({eventId}, 'Concert', now(), now() + interval '1 day', 5, 2)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Reservations" ("Id", "EventId", "SeatCount", "State")
            VALUES ({booking}, {eventId}, 2, 'Held')
            """);

        await db.Database.MigrateAsync();
        var item = await db.Events.SingleAsync();
        Assert.Equal(2, item.HeldSeats);
        Assert.Equal(4, item.AvailableSeats);
        var handler = new ReservationSubscriber(new ReservationRepository(db), NullLogger<ReservationSubscriber>.Instance);
        await handler.Confirm(new BookingConfirmed(booking, eventId, Guid.NewGuid(), 2, DateTimeOffset.UtcNow), default);
        db.ChangeTracker.Clear();
        var confirmed = await db.Events.SingleAsync();
        Assert.Equal(0, confirmed.HeldSeats);
        Assert.Equal(2, confirmed.AvailableSeats);
    }
}
