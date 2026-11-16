using Events.Application.Messaging;
using Events.Application.Services.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Events.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventsService>();
        services.AddScoped<ReservationSubscriber>();
        return services;
    }
}
