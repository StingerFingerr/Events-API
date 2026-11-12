using Bookings.Infrastructure.Messaging;
using Bookings.Application.Messaging;
using System.Text;
using Bookings.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Bookings.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Bookings.Presentation.Extensions;
using Bookings.Infrastructure;
using Bookings.Presentation.Middlewares;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var broker = builder.Configuration["Kafka:BootstrapServers"];
if (string.IsNullOrWhiteSpace(broker))
    throw new InvalidOperationException("Kafka:BootstrapServers is required.");
var group = builder.Configuration["Kafka:ConsumerGroup"];
if (string.IsNullOrWhiteSpace(group))
    throw new InvalidOperationException("Kafka:ConsumerGroup is required.");
builder.Services.AddSingleton<IBookingPublisher>(_ => new KafkaBookingPublisher(broker));
builder.Services.AddScoped<ReservationDecisionHandler>();
builder.Services.AddHostedService<BookingDispatcher>();
builder.Services.AddHostedService(sp => new SeatsDecidedConsumer(broker, group,
    sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILogger<SeatsDecidedConsumer>>()));

builder.Services.AddProblemDetails();
builder.Services.AddControllersWithOptions();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.OperationFilter<BearerSecurityFilter>();
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetRequiredSection(JwtOptions.SectionName));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
{
    var jwt = jwtOptions.Value;
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "unique_name",
        RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured."));

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();
// Validate before database initialization so configuration errors are reported first.
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Database.MigrateAsync();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
