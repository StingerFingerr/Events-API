using Events.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Integration_tests.Events;

[Collection(PostgreSqlCollection.Name)]
public class ReservationNamesMigrationTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task UpgradeAndRollback_PreserveExistingReservationsAndSeatCounts()
    {
        await using var db = Database.CreateContext();
        const string previous = "20261112162307_SeparateHeldSeats";
        await db.Database.MigrateAsync(previous);
        var item = CreateEvent();
        item.TryHoldSeats(2);
        db.Events.Add(item);
        await db.SaveChangesAsync();
        var rows = Enum.GetValues<ReservationState>().Select(state => new SeatReservation
        {
            Id = Guid.NewGuid(), EventId = state == ReservationState.Cancelled ? null : item.Id,
            SeatCount = state == ReservationState.Cancelled ? 0 : 2, State = state
        }).ToArray();
        foreach (var row in rows)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Reservations" ("Id", "EventId", "SeatCount", "State")
                VALUES ({row.Id}, {row.EventId}, {row.SeatCount}, {row.State.ToString()})
                """);

        await db.Database.MigrateAsync();
        await AssertRows();

        await db.Database.MigrateAsync(previous);
        var oldCount = await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::integer AS "Value" FROM "Reservations"
            WHERE "Id" IS NOT NULL AND "SeatCount" >= 0 AND "State" IS NOT NULL
            """).SingleAsync();
        Assert.Equal(rows.Length, oldCount);
        await db.Database.MigrateAsync();
        await AssertRows();

        async Task AssertRows()
        {
            db.ChangeTracker.Clear();
            var saved = await db.Reservations.ToDictionaryAsync(r => r.Id);
            Assert.Equal(rows.Length, saved.Count);
            foreach (var row in rows)
            {
                Assert.Equal(row.EventId, saved[row.Id].EventId);
                Assert.Equal(row.SeatCount, saved[row.Id].SeatCount);
                Assert.Equal(row.State, saved[row.Id].State);
            }
            var savedEvent = await db.Events.SingleAsync();
            Assert.Equal(2, savedEvent.HeldSeats);
            Assert.Equal(item.AvailableSeats, savedEvent.AvailableSeats);
        }
    }

    [Fact]
    public async Task FreshDatabase_UsesSnakeCaseColumnsAndCanStoreReservations()
    {
        await using var db = Database.CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT column_name AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'reservations'
            """).ToListAsync();
        Assert.Equal(new[] { "event_id", "id", "seat_count", "state" }, columns.OrderBy(c => c));
        var row = new SeatReservation { Id = Guid.NewGuid(), State = ReservationState.Cancelled };
        db.Reservations.Add(row);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(ReservationState.Cancelled, (await db.Reservations.SingleAsync()).State);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
