using Domain.Exceptions;
using Domain.Models;
using Infrastructure.Repositories.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Integration_tests;

[Collection(PostgreSqlCollection.Name)]
public class BookingQuotaTransactionTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task ConcurrentTransactions_OnlyOneCanTakeTheLastQuotaSlot()
    {
        var user = User.Create(Guid.NewGuid(), "visitor", "hash");
        var first = CreateEvent("First concert");
        var second = CreateEvent("Second concert");
        await using (var setup = Database.CreateContext())
        {
            setup.Users.Add(user);
            setup.Events.AddRange(first, second);
            for (var i = 0; i < 9; i++)
            {
                Assert.True(first.TryReserveSeats());
                setup.Bookings.Add(Booking.Create(first, user.Id));
            }
            await setup.SaveChangesAsync();
        }

        // Both transactions must see nine bookings before either inserts its tenth.
        var bothHaveRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int readers = 0;
        async Task<bool> TryBookAsync(Guid eventId)
        {
            await using var context = Database.CreateContext();
            var repository = new BookingRepository(context);
            try
            {
                return await repository.RunBookingChangeAsync(async () =>
                {
                    var concert = await context.Events.SingleAsync(e => e.Id == eventId);
                    Assert.Equal(9, await repository.CountActiveAsync(user.Id));
                    if (Interlocked.Increment(ref readers) == 2)
                        bothHaveRead.SetResult();
                    await bothHaveRead.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    Assert.True(concert.TryReserveSeats());
                    await repository.AddAsync(Booking.Create(concert, user.Id));
                    return true;
                });
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryBookAsync(first.Id), TryBookAsync(second.Id));
        Assert.Single(results, succeeded => succeeded);
        await using var verification = Database.CreateContext();
        Assert.Equal(10, await new BookingRepository(verification).CountActiveAsync(user.Id));
        Assert.Equal(10, await verification.Events.SumAsync(e => e.AvailableSeats));
    }
}
