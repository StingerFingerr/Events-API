using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookings.Infrastructure.Messaging;

public sealed class SeatsDecidedConsumer(string bootstrapServers, string consumerGroup, IServiceScopeFactory scopes,
    ILogger<SeatsDecidedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var config = new ConsumerConfig { BootstrapServers = bootstrapServers,
            GroupId = consumerGroup, AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true, TopicMetadataRefreshIntervalMs = 3000, EnableAutoCommit = false };
        while (!stop.IsCancellationRequested)
        {
            using var consumer = new ConsumerBuilder<string, string>(config).Build();
            try
            {
                consumer.Subscribe(BookingTopics.SeatsDecided);
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
                    var decision = JsonSerializer.Deserialize<SeatsDecided>(record.Message.Value)
                        ?? throw new JsonException("Missing decision");
                    await scope.ServiceProvider.GetRequiredService<ReservationDecisionHandler>().Handle(decision, stop);
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
}
