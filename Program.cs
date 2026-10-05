using Events_API.DataAccess;
using Microsoft.EntityFrameworkCore;
using Events_API.Background_tasks.Booking;
using Events_API.Extensions;
using Events_API.Middlewares;
using Events_API.Models;
using Events_API.Services.Bookings;
using Events_API.Services.Events;
using Events_API.Repositories.Events;
using Events_API.Repositories.Bookings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllersWithOptions();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IEventService, EventsService>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IBookingService, BookingsService>();
builder.Services.AddHostedService<BookingBackgroundService>();

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!await db.Events.AnyAsync())
    {
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

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();

app.Run();
