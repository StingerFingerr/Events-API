using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Users.Domain.Models;
using Users.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Test;

public class AuthComponentTests
{
    [Fact]
    public void Hash_UsesUniqueSaltsAndVerifiesPassword()
    {
        var hasher = new PasswordHasher();
        var first = hasher.Hash("пароль 🔒");
        var second = hasher.Hash("пароль 🔒");
        Assert.NotEqual(first, second);
        Assert.StartsWith("PBKDF2-SHA256$600000$", first);
        Assert.False(hasher.NeedsRehash(first));
        Assert.True(hasher.Verify("пароль 🔒", first));
        Assert.True(hasher.Verify("пароль 🔒", second));
        Assert.False(hasher.Verify("wrong", first));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA256$600000$invalid$invalid")]
    [InlineData("PBKDF2-SHA256$999999999$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$600000$!!!!!!!!!!!!!!!!!!!!!!!!$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Verify_MalformedHashReturnsFalse(string? hash)
    {
        Assert.False(new PasswordHasher().Verify("pass", hash!));
    }

    [Fact]
    public void Verify_AcceptsLegacySha256ForUpgrade()
    {
        var hasher = new PasswordHasher();
        const string hash = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
        Assert.True(hasher.NeedsRehash(hash));
        Assert.True(hasher.Verify("abc", hash));
        Assert.False(hasher.Verify("ABC", hash));
        Assert.False(hasher.Verify("abc", new string('X', 64)));
    }

    [Fact]
    public async Task RegisterAndLogin_StoreOnlyHashAndIssueTokenForCorrectPassword()
    {
        using var db = new Users.Infrastructure.DataAccess.UsersDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Users.Infrastructure.DataAccess.UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var passwords = new PasswordHasher();
        var tokens = new Moq.Mock<Users.Application.Services.Auth.IJwtTokenService>();
        tokens.Setup(t => t.Generate(Moq.It.IsAny<User>())).Returns("signed-token");
        var service = new Users.Application.Services.Auth.UserService(
            new Users.Infrastructure.Repositories.Users.UserRepository(db), passwords, tokens.Object);
        var id = await service.RegisterAsync("alice", "pass");
        db.ChangeTracker.Clear();
        var saved = await db.Users.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(UserRole.User, saved.Role);
        Assert.True(passwords.Verify("pass", saved.PasswordHash));
        Assert.False(passwords.NeedsRehash(saved.PasswordHash));
        Assert.Equal("signed-token", await service.LoginAsync("alice", "pass"));
        await Assert.ThrowsAsync<Users.Domain.Exceptions.InvalidCredentialsException>(() => service.LoginAsync("alice", "wrong"));
        await Assert.ThrowsAsync<Users.Domain.Exceptions.InvalidCredentialsException>(() => service.LoginAsync("unknown", "pass"));
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
