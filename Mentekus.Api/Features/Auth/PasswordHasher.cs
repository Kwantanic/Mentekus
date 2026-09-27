using System.Security.Cryptography;

namespace Mentekus.Api.Features.Auth;

internal static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;
    private const string Version = "v1";
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    private static readonly string DummyHash = Hash("mentekus-dummy-password");

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return string.Join('.', Version, Iterations.ToString(), Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(string password, string? stored)
    {
        // A missing hash still runs PBKDF2 so unknown emails do not return faster than wrong passwords.
        if (!TryRead(string.IsNullOrEmpty(stored) ? DummyHash : stored, out var iterations, out var salt, out var expected))
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryRead(string stored, out int iterations, out byte[] salt, out byte[] expected)
    {
        iterations = 0;
        salt = [];
        expected = [];

        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != Version)
            return false;
        if (!int.TryParse(parts[1], out iterations) || iterations is < 1 or > 1_000_000)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && expected.Length > 0;
    }
}
