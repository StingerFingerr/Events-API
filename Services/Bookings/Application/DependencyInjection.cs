using Bookings.Application.Services.Bookings;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingsService>();
        return services;
    }
}
