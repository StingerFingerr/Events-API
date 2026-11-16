using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Events.Domain.Models;
using Events.Presentation.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Integration_tests.Events;

[Collection(PostgreSqlCollection.Name)]
public class EventDatesApiTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Theory]
    [InlineData("Z", 12)]
    [InlineData("+00:00", 12)]
    [InlineData("+03:00", 9)]
    [InlineData("-05:00", 17)]
    public async Task CreateAndUpdate_WithOffset_PersistUtc(string offset, int utcHour)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var created = await client.PostAsJsonAsync("/events", Payload(offset, offset));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        await AssertDates(client, id, utcHour);

        // Change the stored instant before updating, so an ignored update cannot pass.
        using var reset = await client.PutAsJsonAsync($"/events/{id}", Payload("+01:00", "+01:00"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        using var updated = await client.PutAsJsonAsync($"/events/{id}", Payload(offset, offset));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await AssertDates(client, id, utcHour);
    }

    [Theory]
    [InlineData("", "Z", "StartAt")]
    [InlineData("Z", "", "EndAt")]
    public async Task MissingOffset_Returns400WithoutChangingData(string startOffset, string endOffset, string field)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var created = await client.PostAsJsonAsync("/events", Payload("Z", "Z"));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var rejectedCreate = await client.PostAsJsonAsync("/events", Payload(startOffset, endOffset));
        using var rejectedUpdate = await client.PutAsJsonAsync($"/events/{id}", Payload(startOffset, endOffset));
        foreach (var response in new[] { rejectedCreate, rejectedUpdate })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(field, problem.GetProperty("detail").GetString());
            Assert.Contains("time zone", problem.GetProperty("detail").GetString());
        }
        await AssertDates(client, id, 12);
        await using var db = Database.CreateContext();
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Get_PreexistingUtcEvent_PreservesDates()
    {
        var item = Event.Create(Guid.NewGuid(), "Existing event", StartAt, StartAt.AddHours(1), 10);
        await using (var db = Database.CreateContext())
        {
            db.Events.Add(item);
            await db.SaveChangesAsync();
        }
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AssertDates(client, item.Id, 12);
    }

    [Fact]
    public async Task DifferentOffsets_CompareInstantsInsteadOfWallClockTimes()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        // 12:00Z is after 13:00+03:00 (10:00Z).
        using var response = await client.PostAsJsonAsync("/events", Payload("Z", "+03:00"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object Payload(string startOffset, string endOffset) => new
    {
        title = "Offset concert", totalSeats = 10,
        startAt = $"2030-01-01T12:00:00{startOffset}",
        endAt = $"2030-01-01T13:00:00{endOffset}"
    };

    private async Task AssertDates(HttpClient client, Guid id, int utcHour)
    {
        var expected = new DateTime(2030, 1, 1, utcHour, 0, 0, DateTimeKind.Utc);
        var body = await client.GetFromJsonAsync<JsonElement>($"/events/{id}");
        Assert.Equal(expected, body.GetProperty("startAt").GetDateTime());
        Assert.Equal(expected.AddHours(1), body.GetProperty("endAt").GetDateTime());
        Assert.Equal(DateTimeKind.Utc, body.GetProperty("startAt").GetDateTime().Kind);
        await using var db = Database.CreateContext();
        var saved = await db.Events.SingleAsync(e => e.Id == id);
        Assert.Equal(expected, saved.StartAt);
        Assert.Equal(expected.AddHours(1), saved.EndAt);
        Assert.Equal(DateTimeKind.Utc, saved.StartAt.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.EndAt.Kind);
    }

    private const string Secret = "integration-test-only-signing-key-123456789";

    private WebApplicationFactory<EventsController> CreateFactory() =>
        new WebApplicationFactory<EventsController>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", Database.ConnectionString);
            builder.UseSetting("Jwt:Secret", Secret);
            builder.UseSetting("Jwt:Issuer", "date-tests");
            builder.UseSetting("Jwt:Audience", "date-tests");
            // These tests exercise HTTP and PostgreSQL; message consumers are unrelated.
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        });

    private static HttpClient CreateClient(WebApplicationFactory<EventsController> factory)
    {
        var token = new JwtSecurityToken("date-tests", "date-tests",
            [new Claim("role", "Admin")], expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
