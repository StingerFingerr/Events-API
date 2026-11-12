using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookings.Domain.Models;
using Bookings.Presentation.Controllers;
using Events.Presentation.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Kafka;
using Events.Infrastructure.DataAccess;
using Events.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Confluent.Kafka;
using Contracts;
using Npgsql;
using Users.Presentation.Controllers;
using Integration_tests.Events;

namespace Integration_tests;

[Collection(Events.PostgreSqlCollection.Name)]
public class BookingJourneyTests(Events.PostgreSqlFixture database)
{

    private WebApplicationFactory<T> Host<T>(string name, string broker) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name }.ConnectionString);
            builder.UseSetting("Jwt:Secret", "integration-test-only-signing-key-123456789");
            builder.UseSetting("Kafka:BootstrapServers", broker);
        });

    [Fact]
    public async Task LastSeat_ConfirmRejectCancelAndBookAgain()
    {
        await using var kafka = new KafkaBuilder("apache/kafka:3.9.1").WithKRaft()
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false").Build();
        await kafka.StartAsync();
        var broker = kafka.GetBootstrapAddress();
        using var users = Host<AuthController>("journey_users", broker);
        using var events = Host<EventsController>("journey_events", broker);
        using var eventClient = events.CreateClient();
        using var bookings = Host<BookingsController>("journey_bookings", broker);
        using var auth = users.CreateClient();
        using var bookingClient = bookings.CreateClient();
        var credentials = new { login = Guid.NewGuid().ToString(), password = "example", role = "Admin" };
        (await auth.PostAsJsonAsync("/auth/register", credentials)).EnsureSuccessStatusCode();
        using var login = await auth.PostAsJsonAsync("/auth/login", credentials);
        var jwt = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        bookingClient.DefaultRequestHeaders.Authorization = eventClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        var start = DateTime.UtcNow.AddDays(1);
        using var created = await eventClient.PostAsJsonAsync("/events", new { title = "One seat concert",
            totalSeats = 1, startAt = start, endAt = start.AddHours(1) });
        created.EnsureSuccessStatusCode();
        var eventId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<Uri> Book()
        {
            using var response = await bookingClient.PostAsync($"/events/{eventId}/book", null);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            return response.Headers.Location!;
        }
        async Task WaitForStatus(Uri location, BookingStatus status) =>
            await WaitUntil(async () => (await bookingClient.GetFromJsonAsync<JsonElement>(location))
                .GetProperty("status").GetInt32() == (int)status);
        async Task<int> Available() => (await eventClient.GetFromJsonAsync<JsonElement>($"/events/{eventId}"))
            .GetProperty("availableSeats").GetInt32();

        var first = await Book();
        await WaitForStatus(first, BookingStatus.Confirmed);
        using var observer = new ConsumerBuilder<string, string>(new ConsumerConfig {
            BootstrapServers = broker, GroupId = "journey-observer", AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        observer.Subscribe(BookingTopics.BookingConfirmed);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var record = observer.Consume(timeout.Token);
        var confirmed = JsonSerializer.Deserialize<BookingConfirmed>(record.Message.Value)!;
        Assert.Equal(eventId.ToString("D"), record.Message.Key);
        Assert.Equal(Guid.Parse(first.Segments.Last()), confirmed.BookingId);
        Assert.Equal(eventId, confirmed.EventId);
        Assert.Equal(1, confirmed.SeatCount);
        await WaitUntil(async () => await Available() == 0);
        Assert.Equal(HttpStatusCode.Conflict, (await eventClient.DeleteAsync($"/events/{eventId}")).StatusCode);
        var visitor = new { login = Guid.NewGuid().ToString(), password = "example" };
        (await auth.PostAsJsonAsync("/auth/register", visitor)).EnsureSuccessStatusCode();
        using var visitorLogin = await auth.PostAsJsonAsync("/auth/login", visitor);
        using var otherClient = bookings.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherClient.GetAsync(first)).StatusCode);
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await visitorLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.DeleteAsync(first)).StatusCode);
        var second = await Book();
        await WaitForStatus(second, BookingStatus.Rejected);
        (await bookingClient.DeleteAsync(first)).EnsureSuccessStatusCode();
        await WaitUntil(async () => await Available() == 1);
        await WaitForStatus(await Book(), BookingStatus.Confirmed);
    }

    [Fact]
    public async Task StandaloneConfirmations_SkipInvalidRecordsThenDeductAndDeduplicate()
    {
        await using var kafka = new KafkaBuilder("apache/kafka:3.9.1").WithKRaft()
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false").Build();
        await kafka.StartAsync();
        var broker = kafka.GetBootstrapAddress();
        using var host = Host<EventsController>("confirmation_events", broker);
        using var client = host.CreateClient();
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var item = Event.Create(Guid.NewGuid(), "Concert", tomorrow, tomorrow.AddHours(1), 4);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            db.Events.Add(item);
            await db.SaveChangesAsync();
        }

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = broker }).Build();
        // Use one partition so the final valid confirmation proves earlier bad records didn't block progress.
        Task Send(string value) => producer.ProduceAsync(BookingTopics.BookingConfirmed, new Message<string, string>
            { Key = item.Id.ToString(), Value = value });
        BookingConfirmed Confirmation(int seats) => new(Guid.NewGuid(), item.Id, Guid.NewGuid(), seats, DateTimeOffset.UtcNow);
        var missing = Confirmation(1) with { EventId = Guid.NewGuid() };
        var tooLarge = Confirmation(5);
        var twoSeats = Confirmation(2);
        var last = Confirmation(1);
        await Send("not JSON");
        await Send(JsonSerializer.Serialize(Confirmation(0)));
        foreach (var message in new[] { missing, tooLarge, twoSeats, twoSeats, last })
            await Send(JsonSerializer.Serialize(message));

        await WaitUntil(async () => (await client.GetFromJsonAsync<JsonElement>($"/events/{item.Id}"))
            .GetProperty("availableSeats").GetInt32() == 1
            && await LastWasProcessed());

        async Task<bool> LastWasProcessed()
        {
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            return (await db.Reservations.FindAsync(last.BookingId))?.State == ReservationState.Confirmed;
        }

        await using var verification = host.Services.CreateAsyncScope();
        var receipts = verification.ServiceProvider.GetRequiredService<EventsDbContext>().Reservations;
        Assert.Equal(ReservationState.Rejected, (await receipts.FindAsync(missing.BookingId))!.State);
        Assert.Equal(ReservationState.Rejected, (await receipts.FindAsync(tooLarge.BookingId))!.State);
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }
}
