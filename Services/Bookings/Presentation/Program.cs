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

var messagingKey = builder.Configuration["Messaging:Key"];
if (string.IsNullOrWhiteSpace(messagingKey))
    throw new InvalidOperationException("Messaging:Key is required.");
builder.Services.AddHttpClient("events", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Messaging:EventsUrl"]!);
    client.DefaultRequestHeaders.Add("X-Messaging-Key", messagingKey);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService(sp => new Bookings.Infrastructure.Messaging.BookingDispatcher(
    sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IHttpClientFactory>().CreateClient("events"),
    sp.GetRequiredService<ILogger<Bookings.Infrastructure.Messaging.BookingDispatcher>>()));

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
