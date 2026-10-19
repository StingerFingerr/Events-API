using Events_API.DataAccess;
using Events_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Events_API.Infrastructure;

public static class DatabaseInitialization
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Events.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        db.Events.AddRange(
            Event.Create(Guid.NewGuid(), "Рок-фестиваль", "Выступления местных рок-групп.",
                now.AddDays(1), now.AddDays(1).AddHours(5), 100),
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Доклады о C#, ASP.NET Core и EF Core.",
                now.AddDays(7), now.AddDays(7).AddHours(8), 50),
            Event.Create(Guid.NewGuid(), "Кулинарный мастер-класс", "Готовим итальянскую пасту вместе с шеф-поваром.",
                now.AddDays(14), now.AddDays(14).AddHours(2), 10));
        await db.SaveChangesAsync();
    }
}
