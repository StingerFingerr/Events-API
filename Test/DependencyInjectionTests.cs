using Infrastructure.Repositories.Bookings;
using Infrastructure.Repositories.Events;
using Application;
using Application.Services.Events;
using Infrastructure.DataAccess;
using Infrastructure;
using Application.Repositories.Bookings;
using Application.Repositories.Events;
using Application.Services.Bookings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Test;

public class DependencyInjectionTests
{
    [Fact]
    public void LayerRegistrations_ResolveServicesAndWorkerWithScopeValidation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure("Host=localhost;Database=di_validation;Username=test;Password=test");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        Assert.IsType<EventsService>(scope.ServiceProvider.GetRequiredService<IEventService>());
        Assert.IsType<BookingsService>(scope.ServiceProvider.GetRequiredService<IBookingService>());
        Assert.IsType<EventRepository>(scope.ServiceProvider.GetRequiredService<IEventRepository>());
        Assert.IsType<BookingRepository>(scope.ServiceProvider.GetRequiredService<IBookingRepository>());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL",
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ProviderName);
        Assert.Single(provider.GetServices<IHostedService>());
    }
}
