using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Events_API.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration_tests;

[Collection(PostgreSqlCollection.Name)]
public class AuthenticationApiTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task RegistrationAndLogin_EnforceUniqueLoginAndEventPermissions()
    {
        using var factory = new WebApplicationFactory<AuthController>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", Database.ConnectionString);
            builder.UseSetting("Jwt:Secret", "integration-test-only-signing-key-123456789");
        });
        using var client = factory.CreateClient();
        var start = DateTime.UtcNow.AddDays(1);
        var concert = new { title = "Concert", totalSeats = 5, startAt = start, endAt = start.AddHours(2) };
        var user = new { login = "visitor", password = "visitor-pass" };
        var admin = new { login = "manager", password = "manager-pass", role = "Admin" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/events", concert)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/auth/register", user)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/auth/register", user)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/auth/register", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login",
            new { user.login, password = "incorrect" })).StatusCode);

        await LoginAsync(client, user.login, user.password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/events", concert)).StatusCode);

        await LoginAsync(client, admin.login, admin.password);
        var created = await client.PostAsJsonAsync("/events", concert);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var eventId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await LoginAsync(client, user.login, user.password);
        var booking = await client.PostAsync($"/events/{eventId}/book", null);
        Assert.Equal(HttpStatusCode.Accepted, booking.StatusCode);
        var location = Assert.IsType<Uri>(booking.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(location)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(location)).StatusCode);
        await LoginAsync(client, admin.login, admin.password);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(location)).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string login, string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { login, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }
}
