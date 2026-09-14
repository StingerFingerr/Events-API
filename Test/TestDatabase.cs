using Events_API.DataAccess;
using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

internal static class TestDatabase
{
    public static ServiceProvider CreateServiceProvider()
    {
        // Capture one name so all scopes of this provider share the same database.
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventService, EventsService>();
        services.AddScoped<IBookingService, BookingsService>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
