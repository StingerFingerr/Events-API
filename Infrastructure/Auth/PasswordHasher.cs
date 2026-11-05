using System.Security.Cryptography;
using System.Text;
using Application.Services.Auth;

namespace Infrastructure.Auth;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public bool Verify(string password, string passwordHash)
    {
        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        if (passwordHash is null || passwordHash.Length != expected.Length * 2 ||
            Convert.FromHexString(passwordHash, expected, out _, out _) != System.Buffers.OperationStatus.Done)
            return false;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
