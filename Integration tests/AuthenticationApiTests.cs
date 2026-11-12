using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Users.Presentation.Controllers;
using Users.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration_tests.Users;

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

    private static async Task RegisterAsync(HttpClient client, string login = "visitor", string role = "User")
    {
        using var response = await client.PostAsJsonAsync("/auth/register", new { login, password = "pass", role });
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string login, string password) =>
        client.PostAsJsonAsync("/auth/login", new { login, password });
}
