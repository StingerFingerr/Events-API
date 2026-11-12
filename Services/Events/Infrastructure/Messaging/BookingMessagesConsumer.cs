using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Infrastructure.Messaging;

public sealed class BookingMessagesConsumer(string bootstrapServers, IServiceScopeFactory scopes,
    KafkaDecisionPublisher publisher, ILogger<BookingMessagesConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var config = new ConsumerConfig { BootstrapServers = bootstrapServers,
            GroupId = "events-reservations-v1", AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true, TopicMetadataRefreshIntervalMs = 3000, EnableAutoCommit = false };
        while (!stop.IsCancellationRequested)
        {
            using var consumer = new ConsumerBuilder<string, string>(config).Build();
            try
            {
                consumer.Subscribe(new[] { BookingTopics.ReserveSeats, BookingTopics.BookingConfirmed, BookingTopics.BookingCancelled });
                while (!stop.IsCancellationRequested)
                {
                    ConsumeResult<string, string> record;
                    try { record = consumer.Consume(stop); }
                    catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        // A new installation may not have seen its first publication yet.
                        // Stay in the group so the other subscribed topics can make progress.
                        await Task.Delay(TimeSpan.FromSeconds(1), stop);
                        continue;
                    }
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

    private static T Read<T>(ConsumeResult<string, string> record) where T : class =>
        JsonSerializer.Deserialize<T>(record.Message.Value) ?? throw new JsonException("Missing message");
}
