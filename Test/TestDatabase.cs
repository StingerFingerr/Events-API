using Events.Application.Services.Events;
using Events.Infrastructure.DataAccess;
using Events.Application.Repositories.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

internal static class TestDatabase
{
    public static ServiceProvider CreateServiceProvider()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<EventsDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventService, EventsService>();
        services.AddScoped<IEventRepository, InMemoryEventRepository>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
