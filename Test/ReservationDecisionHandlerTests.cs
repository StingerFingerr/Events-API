using Bookings.Application.Messaging;
using Bookings.Domain.Models;
using Bookings.Infrastructure.DataAccess;
using Bookings.Infrastructure.Messaging;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Test;

public class ReservationDecisionHandlerTests
{
    [Fact]
    public async Task FailedPublish_RetriesCommittedConfirmationEvenAfterCancellation()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid());
        await using (var setup = new BookingsDbContext(options))
        {
            setup.Bookings.Add(booking);
            await setup.SaveChangesAsync();
        }

        List<BookingConfirmed> attempts = [];
        var publisher = new Mock<IBookingPublisher>(MockBehavior.Strict);
        publisher.Setup(p => p.Confirm(It.IsAny<BookingConfirmed>(), It.IsAny<CancellationToken>()))
            .Returns(async (BookingConfirmed message, CancellationToken ct) =>
            {
                attempts.Add(message);
                await using var reader = new BookingsDbContext(options);
                var saved = await reader.Bookings.SingleAsync(ct);
                Assert.Equal(saved.UndeliveredConfirmationAt, message.ConfirmedAt.UtcDateTime);
                if (attempts.Count == 1)
                {
                    Assert.Equal(BookingStatus.Confirmed, saved.Status);
                    throw new IOException("Kafka unavailable");
                }
            });

        var decision = new SeatsDecided(booking.Id, true);
        async Task Deliver()
        {
            await using var db = new BookingsDbContext(options);
            await new ReservationDecisionHandler(db, publisher.Object).Handle(decision, default);
        }

        await Assert.ThrowsAsync<IOException>(Deliver);
        await using (var db = new BookingsDbContext(options))
        {
            (await db.Bookings.SingleAsync()).Cancel();
            await db.SaveChangesAsync();
        }
        await Deliver();
        await Deliver();
        Assert.Equal(2, attempts.Count);
        Assert.Equal(attempts[0], attempts[1]);
    }
}
