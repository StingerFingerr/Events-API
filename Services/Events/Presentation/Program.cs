using Events.Infrastructure.Messaging;
using System.Text;
using Events.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Events.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Events.Presentation.Extensions;
using Events.Infrastructure;
using Events.Presentation.Middlewares;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
// Shared defaults have lower priority than service settings, environment variables and command-line arguments.
var jwtDefaults = new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
{
    Path = Path.Combine(AppContext.BaseDirectory, "jwtsettings.json")
};
jwtDefaults.ResolveFileProvider();
builder.Configuration.Sources.Insert(0, jwtDefaults);

var broker = builder.Configuration["Kafka:BootstrapServers"];
if (string.IsNullOrWhiteSpace(broker))
    throw new InvalidOperationException("Kafka:BootstrapServers is required.");
var group = builder.Configuration["Kafka:ConsumerGroup"];
if (string.IsNullOrWhiteSpace(group))
    throw new InvalidOperationException("Kafka:ConsumerGroup is required.");
builder.Services.AddScoped<Events.Application.Messaging.ReservationSubscriber>();
builder.Services.AddSingleton(_ => new KafkaDecisionPublisher(broker));
builder.Services.AddHostedService(sp => new BookingTopicInitializer(broker, sp.GetRequiredService<ILogger<BookingTopicInitializer>>()));
builder.Services.AddHostedService(sp => new BookingMessagesConsumer(broker, group,
    sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<KafkaDecisionPublisher>(),
    sp.GetRequiredService<ILogger<BookingMessagesConsumer>>()));

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
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
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
    await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database.MigrateAsync();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
