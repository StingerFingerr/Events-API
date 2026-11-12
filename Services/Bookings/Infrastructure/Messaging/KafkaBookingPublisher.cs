using System.Text.Json;
using Bookings.Application.Messaging;
using Confluent.Kafka;
using Contracts;

namespace Bookings.Infrastructure.Messaging;

public sealed class KafkaBookingPublisher(string bootstrapServers) : IBookingPublisher, IDisposable
{
    private readonly IProducer<string, string> producer = new ProducerBuilder<string, string>(
        new ProducerConfig { BootstrapServers = bootstrapServers, EnableIdempotence = true, Acks = Acks.All }).Build();

    public Task RequestSeats(ReserveSeats request, CancellationToken ct) =>
        Send(BookingTopics.ReserveSeats, request.EventId, request, ct);

    public Task Confirm(BookingConfirmed confirmation, CancellationToken ct) =>
        Send(BookingTopics.BookingConfirmed, confirmation.EventId, confirmation, ct);

    public Task Cancel(Guid eventId, BookingCancelled cancellation, CancellationToken ct) =>
        Send(BookingTopics.BookingCancelled, eventId, cancellation, ct);

    private Task Send<T>(string topic, Guid eventId, T value, CancellationToken ct) =>
        producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = eventId.ToString("D"), Value = JsonSerializer.Serialize(value)
        }, ct);

    public void Dispose()
    {
        try { producer.Flush(TimeSpan.FromSeconds(5)); }
        finally { producer.Dispose(); }
    }
}
