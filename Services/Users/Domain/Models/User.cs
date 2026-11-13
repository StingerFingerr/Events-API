namespace Users.Domain.Models;

public enum UserRole
{
    User,
    Admin
}

public class User
{
    public Guid Id { get; private set; }
    public string Login { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }

    private User(Guid id, string login, string passwordHash, UserRole role)
    {
        Id = id;
        Login = login;
        PasswordHash = passwordHash;
        Role = role;
    }

    public static User Create(Guid id, string login, string passwordHash, UserRole role = UserRole.User)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        return new User(id, login, passwordHash, role);
    }
}
