using Events_API.Background_tasks.Booking;
using Events_API.Models;
using Events_API.Services.Bookings;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Test;

public class BookingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ConfirmsQueuedBooking()
    {
        var task = new BookingTask { BookingId = Guid.NewGuid(), EventId = Guid.NewGuid() };
        var queue = new InMemoryBookingTaskQueue();
        queue.Enqueue(task);
        var booking = new Booking { Id = task.BookingId, EventId = task.EventId, Status = BookingStatus.Pending };
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bookings = new Mock<IBookingService>();
        bookings
            .Setup(service => service.GetBookingByIdAsync(task.BookingId))
            .ReturnsAsync(booking);
        bookings
            .Setup(service => service.UpdateBookingStatusAsync(task.BookingId, BookingStatus.Confirmed))
            .Callback(processed.SetResult)
            .Returns(Task.CompletedTask);
        var worker = new BookingBackgroundService(
            queue,
            bookings.Object,
            NullLogger<BookingBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(6));
        await worker.StopAsync(CancellationToken.None);

        bookings.Verify(
            service => service.UpdateBookingStatusAsync(task.BookingId, BookingStatus.Confirmed),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsBookingThatIsNoLongerPending()
    {
        var task = new BookingTask { BookingId = Guid.NewGuid(), EventId = Guid.NewGuid() };
        var queue = new InMemoryBookingTaskQueue();
        queue.Enqueue(task);
        var bookings = new Mock<IBookingService>();
        bookings
            .Setup(service => service.GetBookingByIdAsync(task.BookingId))
            .ReturnsAsync(new Booking { Id = task.BookingId, EventId = task.EventId, Status = BookingStatus.Confirmed });
        var worker = new BookingBackgroundService(
            queue,
            bookings.Object,
            NullLogger<BookingBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await worker.StopAsync(CancellationToken.None);

        bookings.Verify(
            service => service.UpdateBookingStatusAsync(It.IsAny<Guid>(), It.IsAny<BookingStatus>()),
            Times.Never);
    }
}
