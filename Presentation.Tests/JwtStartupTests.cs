using Infrastructure;
using Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Presentation.Tests;

public class JwtStartupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("                                ")]
    [InlineData("short-secret")]
    [InlineData("1234567890123456789012345678901")]
    public async Task Start_RejectsInvalidSecretWithoutResolvingTokenService(string? secret)
    {
        using var host = CreateHost(secret);

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Jwt:Secret must contain at least 32 UTF-8 bytes.", error.Failures);
    }

    [Theory]
    [InlineData("", "audience", 60, "Jwt:Issuer and Jwt:Audience are required.")]
    [InlineData("issuer", "", 60, "Jwt:Issuer and Jwt:Audience are required.")]
    [InlineData("issuer", "audience", 0, "Jwt:LifetimeMinutes must be positive.")]
    public async Task Start_RejectsInvalidMetadata(string issuer, string audience, int lifetime, string message)
    {
        using var host = CreateHost(new string('a', 32), issuer, audience, lifetime);

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(message, error.Failures);
    }

    [Theory]
    [InlineData("12345678901234567890123456789012")]
    [InlineData("абвгдежзийклмноп")]
    public async Task Start_AcceptsSecretWithAtLeast32Utf8Bytes(string secret)
    {
        using var host = CreateHost(secret);

        await host.StartAsync();
        await host.StopAsync();
    }

    private static IHost CreateHost(string? secret, string issuer = "issuer", string audience = "audience", int lifetime = 60)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInfrastructure("Host=localhost;Database=jwt_validation;Username=test;Password=test");
        // Isolate startup validation from the database-backed booking worker.
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.Configure<JwtOptions>(options =>
        {
            if (secret is not null)
                options.Secret = secret;
            options.Issuer = issuer;
            options.Audience = audience;
            options.LifetimeMinutes = lifetime;
        });
        return builder.Build();
    }
}
