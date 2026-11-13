using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Infrastructure.Messaging;

public sealed class BookingTopicInitializer(string bootstrapServers, ILogger<BookingTopicInitializer> logger)
    : BackgroundService
{
    private bool ready;

    public override async Task StartAsync(CancellationToken ct)
    {
        // Finish the first, bounded attempt before the consumer starts.
        ready = await EnsureTopics(ct);
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!ready)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stop);
            ready = await EnsureTopics(stop);
        }
    }

    private async Task<bool> EnsureTopics(CancellationToken ct)
    {
        string[] names = [BookingTopics.BookingConfirmed, BookingTopics.ReserveSeats,
            BookingTopics.SeatsDecided, BookingTopics.BookingCancelled];
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
            await admin.CreateTopicsAsync(names.Select(name => new TopicSpecification
            {
                Name = name,
                NumPartitions = -1,
                ReplicationFactor = -1 // Use the broker defaults.
            }), new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(5) }).WaitAsync(ct);
            return true;
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r =>
            r.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
        {
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not initialize booking topics; starting the subscriber and retrying later");
            return false;
        }
    }
}
