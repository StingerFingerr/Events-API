using Application.Services.Events;
using Application.Services.Bookings;
using Application.Services.Bookings.Processing;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Services.Auth.IUserService, Services.Auth.UserService>();
        services.AddScoped<IEventService, EventsService>();
        services.AddScoped<IBookingService, BookingsService>();
        services.AddScoped<IPendingBookingsProcessor, PendingBookingsProcessor>();
        return services;
    }
}
