using Users.Application.Repositories.Users;
using Users.Domain.Exceptions;
using Users.Domain.Models;
using Users.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Users.Infrastructure.Repositories.Users;

public class UserRepository(UsersDbContext context) : IUserRepository
{
    public Task<User?> FindByLoginAsync(string login) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Login == login);

    public async Task AddAsync(User user)
    {
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_Login" })
        {
            context.Entry(user).State = EntityState.Detached;
            throw new ConflictException("Login is already registered.");
        }
    }
}
