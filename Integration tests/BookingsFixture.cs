using Bookings.Infrastructure.DataAccess;
using Bookings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Integration_tests.Bookings;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "Bookings PostgreSQL";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("bookings_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public string ConnectionString => _container.GetConnectionString();

    public BookingsDbContext CreateContext() => new(new DbContextOptionsBuilder<BookingsDbContext>()
        .UseNpgsql(ConnectionString)
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
}
