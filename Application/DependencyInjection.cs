using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Events_API.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventsService>();
        services.AddScoped<IBookingService, BookingsService>();
        return services;
    }
}
