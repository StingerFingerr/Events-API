using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;

namespace Events_API.Background_tasks.Booking;

public class BookingBackgroundService(
    IBookingsRepository bookingsRepository,
    IEventsRepository eventsRepository,
    ILogger<BookingBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ExternalCallDelay = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var bookings = bookingsRepository.Bookings.Values
                .Where(booking => booking.Status == BookingStatus.Pending)
                .OrderBy(booking => booking.CreatedAt)
                .Take(BatchSize)
                .ToArray();

            if (bookings.Length == 0)
            {
                try
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            var processingTasks = bookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));

            try
            {
                await Task.WhenAll(processingTasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBookingAsync(Events_API.Models.Booking booking, CancellationToken stoppingToken)
    {
        Event? eventData = null;

        try
        {
            logger.LogInformation("Processing booking {BookingId} for event {EventId}", booking.Id, booking.EventId);
            await Task.Delay(ExternalCallDelay, stoppingToken);

            await _processingSemaphore.WaitAsync(stoppingToken);
            try
            {
                if (!eventsRepository.Events.TryGetValue(booking.EventId, out eventData))
                {
                    booking.Reject();
                    booking.ReleaseReservedSeat();
                    bookingsRepository.Bookings[booking.Id] = booking;
                    logger.LogWarning("Booking {BookingId} rejected because event {EventId} was deleted",
                        booking.Id, booking.EventId);
                    return;
                }

                booking.Confirm();
                bookingsRepository.Bookings[booking.Id] = booking;
                logger.LogInformation("Booking {BookingId} confirmed", booking.Id);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _processingSemaphore.WaitAsync(CancellationToken.None);
            try
            {
                booking.Reject();
                booking.ReleaseReservedSeat(eventData);
                bookingsRepository.Bookings[booking.Id] = booking;

                if (eventData is not null && eventsRepository.Events.ContainsKey(eventData.Id))
                    eventsRepository.Events[eventData.Id] = eventData;
            }
            finally
            {
                _processingSemaphore.Release();
            }

            logger.LogError(ex, "Booking {BookingId} rejected because processing failed", booking.Id);
        }
    }
}
