using System.Text.Json;
using Confluent.Kafka;
using Contracts;

namespace Events.Infrastructure.Messaging;

public sealed class KafkaDecisionPublisher(string bootstrapServers) : IDisposable
{
    private readonly IProducer<string, string> producer = new ProducerBuilder<string, string>(
        new ProducerConfig { BootstrapServers = bootstrapServers, EnableIdempotence = true }).Build();

    public Task Send(Guid eventId, SeatsDecided decision, CancellationToken ct) =>
        producer.ProduceAsync(BookingTopics.SeatsDecided, new Message<string, string>
        { Key = eventId.ToString("D"), Value = JsonSerializer.Serialize(decision) }, ct);

    public void Dispose()
    {
        try { producer.Flush(TimeSpan.FromSeconds(5)); }
        finally { producer.Dispose(); }
    }
}
