using Events_API.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Test;

internal static class TestDatabase
{
    public static AppDbContext Create(string? name = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);
}
