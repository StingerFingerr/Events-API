using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Events_API.Controllers;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration_tests;

[Collection(PostgreSqlCollection.Name)]
public class AuthenticationApiTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public async Task Register_NewLogin_ReturnsNoContent(string role)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { login = "visitor", password = "pass", role });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Register_ExistingLogin_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/auth/register", new { login = "visitor", password = "pass" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client);

        var response = await LoginAsync(client, "visitor", "pass");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
    }

    [Theory]
    [InlineData("visitor", "incorrect")]
    [InlineData("unknown-user", "pass")]
    public async Task Login_InvalidCredentials_ReturnsBadRequest(string login, string password)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client);

        var response = await LoginAsync(client, login, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task CreateEvent_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/events", Concert());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task CreateEvent_RegularUser_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client);
        await AuthenticateAsync(client, "visitor");

        var response = await client.PostAsJsonAsync("/events", Concert());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task CreateEvent_Admin_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "manager", "Admin");
        await AuthenticateAsync(client, "manager");

        var response = await client.PostAsJsonAsync("/events", Concert());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BookEvent_AuthenticatedUser_ReturnsAcceptedWithLocation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var eventId = await SeedEventAsync();
        await RegisterAsync(client);
        await AuthenticateAsync(client, "visitor");

        var response = await client.PostAsync($"/events/{eventId}/book", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/bookings/{id}", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task GetBooking_Owner_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var location = await PrepareBookingAsync(client);

        var response = await client.GetAsync(location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task AccessBooking_WithoutToken_ReturnsUnauthorized(string method)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var location = await PrepareBookingAsync(client);
        client.DefaultRequestHeaders.Authorization = null;

        using var request = new HttpRequestMessage(new HttpMethod(method), location);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task CancelBooking_AnotherUser_ReturnsForbiddenWithoutCancellingOrReleasingSeat()
    {
        using var factory = CreateFactory();
        using var ownerClient = factory.CreateClient();
        var location = await PrepareBookingAsync(ownerClient);
        var bookingId = Guid.Parse(location.ToString().TrimEnd('/').Split('/')[^1]);
        await using var beforeContext = Database.CreateContext();
        var original = await beforeContext.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);
        var availableSeats = (await beforeContext.Events.AsNoTracking().SingleAsync(e => e.Id == original.EventId)).AvailableSeats;

        using var otherClient = factory.CreateClient();
        await RegisterAsync(otherClient, "other-user");
        await AuthenticateAsync(otherClient, "other-user");

        var response = await otherClient.DeleteAsync(location);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, problem.GetProperty("status").GetInt32());

        await using var afterContext = Database.CreateContext();
        var saved = await afterContext.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(original.UserId, saved.UserId);
        // The worker may confirm a pending booking while the HTTP request runs.
        Assert.NotEqual(BookingStatus.Cancelled, saved.Status);
        Assert.Equal(availableSeats, (await afterContext.Events.AsNoTracking().SingleAsync(e => e.Id == original.EventId)).AvailableSeats);
    }

    [Fact]
    public async Task CancelBooking_Admin_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var location = await PrepareBookingAsync(client);
        await RegisterAsync(client, "manager", "Admin");
        await AuthenticateAsync(client, "manager");

        var response = await client.DeleteAsync(location);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Register_MissingCredentials_ReturnsValidationProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
        var errors = problem.GetProperty("errors");
        Assert.True(errors.GetProperty("Login").GetArrayLength() > 0);
        Assert.True(errors.GetProperty("Password").GetArrayLength() > 0);
        Assert.False(problem.TryGetProperty("message", out _));
    }

    private WebApplicationFactory<AuthController> CreateFactory() =>
        new WebApplicationFactory<AuthController>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", Database.ConnectionString);
            builder.UseSetting("Jwt:Secret", "integration-test-only-signing-key-123456789");
        });

    private static object Concert()
    {
        var start = DateTime.UtcNow.AddDays(1);
        return new { title = "Concert", totalSeats = 5, startAt = start, endAt = start.AddHours(2) };
    }

    private async Task<Guid> SeedEventAsync()
    {
        await using var context = Database.CreateContext();
        var concert = CreateEvent();
        context.Events.Add(concert);
        await context.SaveChangesAsync();
        return concert.Id;
    }

    private static async Task RegisterAsync(HttpClient client, string login = "visitor", string role = "User")
    {
        using var response = await client.PostAsJsonAsync("/auth/register", new { login, password = "pass", role });
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string login, string password) =>
        client.PostAsJsonAsync("/auth/login", new { login, password });

    private static async Task AuthenticateAsync(HttpClient client, string login)
    {
        using var response = await LoginAsync(client, login, "pass");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }

    private async Task<Uri> PrepareBookingAsync(HttpClient client)
    {
        var eventId = await SeedEventAsync();
        await RegisterAsync(client);
        await AuthenticateAsync(client, "visitor");
        using var response = await client.PostAsync($"/events/{eventId}/book", null);
        response.EnsureSuccessStatusCode();
        return response.Headers.Location ?? throw new InvalidOperationException("Booking setup returned no Location.");
    }
}
