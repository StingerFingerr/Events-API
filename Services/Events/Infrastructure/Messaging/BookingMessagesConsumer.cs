using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Infrastructure.Messaging;

public sealed class BookingMessagesConsumer(string bootstrapServers, string consumerGroup, IServiceScopeFactory scopes,
    KafkaDecisionPublisher publisher, ILogger<BookingMessagesConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var config = new ConsumerConfig { BootstrapServers = bootstrapServers,
            GroupId = consumerGroup, AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = false, TopicMetadataRefreshIntervalMs = 3000, EnableAutoCommit = false };
        while (!stop.IsCancellationRequested)
        {
            using var consumer = new ConsumerBuilder<string, string>(config).Build();
            try
            {
                consumer.Subscribe(new[] { BookingTopics.ReserveSeats, BookingTopics.BookingConfirmed, BookingTopics.BookingCancelled });
                while (!stop.IsCancellationRequested)
                {
                    ConsumeResult<string, string> record;
                    // On .NET 10 ExecuteAsync runs off the startup thread; cancellation interrupts Consume.
                    try { record = consumer.Consume(stop); }
                    catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        // A new installation may not have seen its first publication yet.
                        // Stay in the group so the other subscribed topics can make progress.
                        await Task.Delay(TimeSpan.FromSeconds(1), stop);
                        continue;
                    }
                    try { await Dispatch(record, stop); }
                    catch (JsonException ex)
                    {
                        logger.LogWarning(ex, "Skipping invalid booking message at {Position}", record.TopicPartitionOffset);
                    }
                    consumer.Commit(record);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning(error, "Restarting consumer from last committed offset");
                await Task.Delay(TimeSpan.FromSeconds(1), stop);
            }
            finally { consumer.Close(); }
        }
    }

    private async Task Dispatch(ConsumeResult<string, string> record, CancellationToken stop)
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReservationSubscriber>();
        switch (record.Topic)
        {
            case BookingTopics.ReserveSeats:
                var request = Read<ReserveSeats>(record);
                var result = await handler.Reserve(request, stop);
                await publisher.Send(request.EventId, new SeatsDecided(request.BookingId, result.Accepted), stop);
                break;
            case BookingTopics.BookingConfirmed:
                await handler.Confirm(Read<BookingConfirmed>(record), stop);
                break;
            case BookingTopics.BookingCancelled:
                await handler.Cancel(Read<BookingCancelled>(record), stop);
                break;
        }
    }

    private static T Read<T>(ConsumeResult<string, string> record) where T : class
    {
        if (record.Message.Value == null) throw new JsonException("Missing booking message");
        var message = JsonSerializer.Deserialize<T>(record.Message.Value);
        var valid = message switch
        {
            BookingConfirmed m => m.BookingId != Guid.Empty && m.EventId != Guid.Empty && m.UserId != Guid.Empty
                && m.SeatCount > 0 && m.ConfirmedAt != default,
            ReserveSeats m => m.BookingId != Guid.Empty && m.EventId != Guid.Empty && m.SeatCount > 0,
            BookingCancelled m => m.BookingId != Guid.Empty,
            _ => false
        };
        return valid ? message! : throw new JsonException("Invalid booking message fields");
    }
}
