using System.Security.Cryptography;
using System.Text;
using Users.Application.Services.Auth;

namespace Users.Infrastructure.Auth;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600_000;
    private const string Prefix = "PBKDF2-SHA256$600000$";

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return Prefix + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    public bool NeedsRehash(string passwordHash) => !passwordHash.StartsWith(Prefix, StringComparison.Ordinal);

    public bool Verify(string password, string passwordHash)
    {
        if (passwordHash is null) return false;
        if (passwordHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var parts = passwordHash[Prefix.Length..].Split('$');
            if (parts.Length != 2 || parts[0].Length != 24 || parts[1].Length != 44) return false;
            Span<byte> salt = stackalloc byte[16];
            Span<byte> hash = stackalloc byte[32];
            if (!Convert.TryFromBase64String(parts[0], salt, out var saltLength) || saltLength != salt.Length ||
                !Convert.TryFromBase64String(parts[1], hash, out var hashLength) || hashLength != hash.Length)
                return false;
            var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(derived, hash);
        }

        // Legacy SHA-256 is accepted only for verification; successful login upgrades it.
        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        if (passwordHash is null || passwordHash.Length != expected.Length * 2 ||
            Convert.FromHexString(passwordHash, expected, out _, out _) != System.Buffers.OperationStatus.Done)
            return false;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
