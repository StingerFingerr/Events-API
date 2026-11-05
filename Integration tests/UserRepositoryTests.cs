using Domain.Exceptions;
using Domain.Models;
using Infrastructure.Repositories.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Integration_tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepositoryTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task AddAsync_PersistsAllFields(UserRole role)
    {
        var user = User.Create(Guid.NewGuid(), "visitor", "password-hash", role);
        await using (var context = Database.CreateContext())
            await new UserRepository(context).AddAsync(user);

        await using var verification = Database.CreateContext();
        var saved = await verification.Users.SingleAsync();
        Assert.Equal(user.Id, saved.Id);
        Assert.Equal(user.Login, saved.Login);
        Assert.Equal(user.PasswordHash, saved.PasswordHash);
        Assert.Equal(role, saved.Role);
    }

    [Fact]
    public async Task FindByLoginAsync_ExistingLogin_ReturnsUntrackedUser()
    {
        var user = User.Create(Guid.NewGuid(), "visitor", "password-hash", UserRole.Admin);
        await using (var seed = Database.CreateContext())
            await new UserRepository(seed).AddAsync(user);
        await using var context = Database.CreateContext();

        var found = await new UserRepository(context).FindByLoginAsync(user.Login);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal(user.Login, found.Login);
        Assert.Equal(user.PasswordHash, found.PasswordHash);
        Assert.Equal(user.Role, found.Role);
        Assert.Equal(EntityState.Detached, context.Entry(found).State);
    }

    [Fact]
    public async Task FindByLoginAsync_UnknownLogin_ReturnsNull()
    {
        await using var context = Database.CreateContext();

        var found = await new UserRepository(context).FindByLoginAsync("unknown");

        Assert.Null(found);
    }

    [Fact]
    public async Task AddAsync_DuplicateLogin_ThrowsConflictAndPreservesOriginal()
    {
        var original = User.Create(Guid.NewGuid(), "visitor", "original-hash");
        await using (var seed = Database.CreateContext())
            await new UserRepository(seed).AddAsync(original);
        await using var context = Database.CreateContext();
        var duplicate = User.Create(Guid.NewGuid(), original.Login, "different-hash", UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(() => new UserRepository(context).AddAsync(duplicate));

        Assert.Equal(EntityState.Detached, context.Entry(duplicate).State);
        await using var verification = Database.CreateContext();
        var saved = await verification.Users.SingleAsync();
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(original.PasswordHash, saved.PasswordHash);
        Assert.Equal(original.Role, saved.Role);
    }

    [Fact]
    public async Task SaveChanges_DuplicateLogin_IsRejectedByUniqueLoginIndex()
    {
        await using (var seed = Database.CreateContext())
            await new UserRepository(seed).AddAsync(User.Create(Guid.NewGuid(), "visitor", "hash"));
        await using var context = Database.CreateContext();
        // Bypass repository error mapping to verify the migrated PostgreSQL constraint itself.
        context.Users.Add(User.Create(Guid.NewGuid(), "visitor", "other-hash"));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_Users_Login", postgres.ConstraintName);
    }
}
