using Events_API.DataAccess;
using Events_API.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Integration_tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL repositories";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("events_repository_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_container.GetConnectionString())
        .Options);

    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }
}

public abstract class RepositoryTestBase(PostgreSqlFixture database) : IAsyncLifetime
{
    protected PostgreSqlFixture Database { get; } = database;
    protected static readonly DateTime StartAt = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => Database.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    protected static Event CreateEvent(string title = "Rock Festival", int day = 0) =>
        Event.Create(Guid.NewGuid(), title, "Description", StartAt.AddDays(day), StartAt.AddDays(day).AddHours(2), 10);

    protected static Booking CreateBooking(Event eventData, BookingStatus status = BookingStatus.Pending, int minute = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventData.Id,
            Event = eventData,
            CreatedAt = StartAt.AddMinutes(minute),
            Status = status
        };
}
