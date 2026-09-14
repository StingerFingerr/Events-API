using Events_API.DataAccess;
using Microsoft.EntityFrameworkCore;
using Events_API.Background_tasks.Booking;
using Events_API.Extensions;
using Events_API.Middlewares;
using Events_API.Services.Bookings;
using Events_API.Services.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllersWithOptions();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IEventService, EventsService>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IBookingService, BookingsService>();
builder.Services.AddHostedService<BookingBackgroundService>();

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();

app.Run();
