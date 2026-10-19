using Application.Services.Events;
using Application.Services.Bookings;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventsService>();
        services.AddScoped<IBookingService, BookingsService>();
        return services;
    }
}
