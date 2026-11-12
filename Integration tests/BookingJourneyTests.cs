using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookings.Domain.Models;
using Bookings.Presentation.Controllers;
using Events.Presentation.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Users.Presentation.Controllers;
using Integration_tests.Events;

namespace Integration_tests;

[Collection(Events.PostgreSqlCollection.Name)]
public class BookingJourneyTests(Events.PostgreSqlFixture database)
{
    private const string MessagingKey = "test-delivery-key";

    private WebApplicationFactory<T> Host<T>(string name) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name }.ConnectionString);
            builder.UseSetting("Jwt:Secret", "integration-test-only-signing-key-123456789");
            builder.UseSetting("Messaging:Key", MessagingKey);
        });

    [Fact]
    public async Task LastSeat_ConfirmRejectCancelAndBookAgain()
    {
        using var users = Host<AuthController>("journey_users");
        using var events = Host<EventsController>("journey_events");
        using var eventClient = events.CreateClient();
        using var bookings = Host<BookingsController>("journey_bookings").WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddHttpClient("events")
                .ConfigurePrimaryHttpMessageHandler(() => events.Server.CreateHandler())));
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
        Assert.Equal(0, await Available());
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

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }
}
