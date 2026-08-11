using Events_API.Models;
using Events_API.Services.Bookings;

namespace Events_API.Background_tasks.Booking;

public class BookingBackgroundService(
    IBookingTaskQueue bookingTaskQueue,
    IBookingService bookingService,
    ILogger<BookingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!bookingTaskQueue.TryDequeue(out var task))
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                var booking = await bookingService.GetBookingByIdAsync(task.BookingId);
                if (booking.Status != BookingStatus.Pending)
                {
                    logger.LogInformation(
                        "Skipping booking {BookingId}: current status is {BookingStatus}",
                        task.BookingId,
                        booking.Status);
                    continue;
                }

                logger.LogInformation("Processing booking {BookingId} for event {EventId}", task.BookingId, task.EventId);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // fake long operation
                await bookingService.UpdateBookingStatusAsync(task.BookingId, BookingStatus.Confirmed);
                logger.LogInformation("Booking {BookingId} confirmed", task.BookingId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process booking {BookingId}", task.BookingId);

                try
                {
                    await bookingService.UpdateBookingStatusAsync(task.BookingId, BookingStatus.Rejected);
                }
                catch (Exception statusUpdateException)
                {
                    logger.LogError(statusUpdateException, "Failed to reject booking {BookingId}", task.BookingId);
                }
            }
        }
    }
}
