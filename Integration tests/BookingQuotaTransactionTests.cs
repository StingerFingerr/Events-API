using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;
using Bookings.Infrastructure.Repositories.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Integration_tests.Bookings;

[Collection(PostgreSqlCollection.Name)]
public class BookingQuotaTransactionTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task ConcurrentTransactions_OnlyOneCanTakeTheLastQuotaSlot()
    {
        var userId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await using (var setup = Database.CreateContext())
        {
            for (var i = 0; i < 9; i++)
            {
                setup.Bookings.Add(Booking.Create(first, userId));
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
                    Assert.Equal(9, await repository.CountActiveAsync(userId));
                    if (Interlocked.Increment(ref readers) == 2)
                        bothHaveRead.SetResult();
                    await bothHaveRead.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    await repository.AddAsync(Booking.Create(eventId, userId));
                    return true;
                });
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryBookAsync(first), TryBookAsync(second));
        Assert.Single(results, succeeded => succeeded);
        await using var verification = Database.CreateContext();
        Assert.Equal(10, await new BookingRepository(verification).CountActiveAsync(userId));
    }
}
