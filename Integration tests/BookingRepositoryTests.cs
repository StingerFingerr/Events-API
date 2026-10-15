using Events_API.Models;
using Events_API.Repositories.Bookings;
using Events_API.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Integration_tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BookingRepositoryTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task AddAsync_PersistsBookingAndReservedSeatTogether()
    {
        var eventData = CreateEvent();
        var booking = CreateBooking(eventData);
        await using (var context = Database.CreateContext())
        {
            await new EventRepository(context).AddAsync(eventData);
            Assert.True(eventData.TryReserveSeats());
            await new BookingRepository(context).AddAsync(booking);
        }

        await using var verification = Database.CreateContext();
        var saved = await verification.Bookings.SingleAsync();
        Assert.Equal(booking.Id, saved.Id);
        Assert.Equal(eventData.Id, saved.EventId);
        Assert.Equal(booking.CreatedAt, saved.CreatedAt);
        Assert.Equal(BookingStatus.Pending, saved.Status);
        Assert.Null(saved.ProcessedAt);
        Assert.Equal(9, (await verification.Events.SingleAsync()).AvailableSeats);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTrackedBookingOrNull()
    {
        var booking = CreateBooking(CreateEvent());
        await using (var seed = Database.CreateContext())
            await new BookingRepository(seed).AddAsync(booking);

        await using var context = Database.CreateContext();
        var repository = new BookingRepository(context);
        var found = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(found);
        Assert.Equal(booking.EventId, found.EventId);
        Assert.Equal(booking.CreatedAt, found.CreatedAt);
        Assert.Equal(BookingStatus.Pending, found.Status);
        Assert.Equal(EntityState.Unchanged, context.Entry(found).State);
        Assert.Same(found, await repository.GetByIdAsync(booking.Id));
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(100, 3)]
    public async Task GetPendingIdsAsync_FiltersOrdersAndLimitsBatch(int batchSize, int expectedCount)
    {
        var eventData = CreateEvent();
        var pending = new[] { CreateBooking(eventData, minute: 0), CreateBooking(eventData, minute: 2), CreateBooking(eventData, minute: 4) };
        await using (var seed = Database.CreateContext())
        {
            seed.Bookings.AddRange(pending.Reverse());
            seed.Bookings.Add(CreateBooking(eventData, BookingStatus.Confirmed, -1));
            seed.Bookings.Add(CreateBooking(eventData, BookingStatus.Rejected, 1));
            await seed.SaveChangesAsync();
        }

        await using var context = Database.CreateContext();
        var ids = await new BookingRepository(context).GetPendingIdsAsync(batchSize);
        Assert.Equal(pending.Take(expectedCount).Select(b => b.Id), ids);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPendingIdsAsync_ReturnsEmptyWhenNoPendingBookings(bool seedProcessed)
    {
        await using var context = Database.CreateContext();
        var repository = new BookingRepository(context);
        if (seedProcessed)
            await repository.AddAsync(CreateBooking(CreateEvent(), BookingStatus.Confirmed));
        Assert.Empty(await repository.GetPendingIdsAsync(100));
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Pending)]
    public async Task SaveChangesAsync_PersistsStatusAndProcessedAt(BookingStatus status)
    {
        var booking = CreateBooking(CreateEvent(), BookingStatus.Confirmed);
        booking.ProcessedAt = StartAt;
        await using (var seed = Database.CreateContext())
            await new BookingRepository(seed).AddAsync(booking);

        var processedAt = status == BookingStatus.Pending ? (DateTime?)null : StartAt.AddMinutes(1);
        await using (var context = Database.CreateContext())
        {
            var repository = new BookingRepository(context);
            var tracked = (await repository.GetByIdAsync(booking.Id))!;
            tracked.Status = status;
            tracked.ProcessedAt = processedAt;
            await repository.SaveChangesAsync();
        }

        await using var verification = Database.CreateContext();
        var saved = await verification.Bookings.SingleAsync();
        Assert.Equal(status, saved.Status);
        Assert.Equal(processedAt, saved.ProcessedAt);
        Assert.Equal(booking.CreatedAt, saved.CreatedAt);
    }

    [Fact]
    public async Task AddAsync_MissingEventFailsForeignKeyAndDetachesBooking()
    {
        await using var context = Database.CreateContext();
        var booking = new Booking { Id = Guid.NewGuid(), EventId = Guid.NewGuid(), CreatedAt = StartAt, Status = BookingStatus.Pending };
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => new BookingRepository(context).AddAsync(booking));
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
        Assert.Equal("FK_bookings_events_event_id", postgresException.ConstraintName);
        Assert.Equal(EntityState.Detached, context.Entry(booking).State);
        await using var verification = Database.CreateContext();
        Assert.Empty(await verification.Bookings.ToListAsync());
    }

    [Fact]
    public async Task AddAsync_FailedInsertRollsBackReservedSeatUpdate()
    {
        var original = CreateBooking(CreateEvent());
        await using (var seed = Database.CreateContext())
            await new BookingRepository(seed).AddAsync(original);

        await using var context = Database.CreateContext();
        var eventData = (await new EventRepository(context).GetByIdAsync(original.EventId))!;
        Assert.True(eventData.TryReserveSeats());
        var duplicate = new Booking { Id = original.Id, EventId = eventData.Id, Event = eventData, CreatedAt = StartAt, Status = BookingStatus.Pending };
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => new BookingRepository(context).AddAsync(duplicate));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        Assert.Equal(EntityState.Detached, context.Entry(duplicate).State);

        await using var verification = Database.CreateContext();
        Assert.Single(await verification.Bookings.ToListAsync());
        Assert.Equal(10, (await verification.Events.SingleAsync()).AvailableSeats);
    }
}
