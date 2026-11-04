using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Domain.Models;
using Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Test;

public class AuthComponentTests
{
    [Fact]
    public void Verify_ChecksSha256HexDigest()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("abc");
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        Assert.True(hasher.Verify("abc", hash));
        Assert.False(hasher.Verify("ABC", hash));
        Assert.False(hasher.Verify("abc", new string('X', 64)));
    }

    [Fact]
    public void Generate_SignsUserClaimsWithConfiguredLifetime()
    {
        var settings = new JwtOptions
        {
            Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Issuer = "issuer",
            Audience = "audience",
            LifetimeMinutes = 15
        };
        var user = User.Create(Guid.NewGuid(), "alice", "hash", UserRole.Admin);
        var token = new JwtTokenService(Options.Create(settings)).Generate(user);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        }, out var validated);
        Assert.Equal(user.Id.ToString(), principal.FindFirst("sub")?.Value);
        Assert.Equal(user.Login, principal.FindFirst("unique_name")?.Value);
        Assert.Equal("Admin", principal.FindFirst("role")?.Value);
        Assert.Equal(TimeSpan.FromMinutes(15), validated.ValidTo - validated.ValidFrom);
    }
}
