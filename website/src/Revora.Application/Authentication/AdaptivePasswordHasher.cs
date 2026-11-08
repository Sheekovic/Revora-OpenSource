using System.Security.Cryptography;

namespace Revora.Application.Authentication;

public sealed class AdaptivePasswordHasher
{
    public const int CurrentVersion = 1;
    private const int Iterations = 600_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ValidatePassword(password);
        return HashValidated(password);
    }

    public string HashLegacyCompatible(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length is < 8 or > 15 || password.Any(static value => value is < '!' or > '~'))
        {
            throw new ArgumentException("Legacy-compatible passwords must contain 8 to 15 printable ASCII characters.", nameof(password));
        }

        return HashValidated(password);
    }

    public string HashSupportingFactor(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is < 2 or > 256) throw new ArgumentException("Supporting factors must contain 2 to 256 characters.", nameof(value));
        return HashValidated(value);
    }

    private static string HashValidated(string password)
    {
        Span<byte> salt = stackalloc byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        try
        {
            return $"pbkdf2-sha512$v={CurrentVersion}$i={Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    public PasswordVerification Verify(string password, string encoded)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encoded))
        {
            PerformDummyWork(password ?? string.Empty);
            return PasswordVerification.Failed;
        }

        string[] parts = encoded.Split('$');
        if (parts.Length != 5 || parts[0] != "pbkdf2-sha512" ||
            !TryReadPositive(parts[1], "v=", out int version) ||
            !TryReadPositive(parts[2], "i=", out int iterations) ||
            iterations is < 100_000 or > 5_000_000)
        {
            PerformDummyWork(password);
            return PasswordVerification.Failed;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[3]);
            byte[] expected = Convert.FromBase64String(parts[4]);
            if (salt.Length is < 16 or > 64 || expected.Length != HashSize)
            {
                return PasswordVerification.Failed;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
            try
            {
                bool valid = CryptographicOperations.FixedTimeEquals(actual, expected);
                return valid
                    ? version == CurrentVersion && iterations == Iterations ? PasswordVerification.Valid : PasswordVerification.ValidNeedsRehash
                    : PasswordVerification.Failed;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(salt);
            }
        }
        catch (FormatException)
        {
            PerformDummyWork(password);
            return PasswordVerification.Failed;
        }
    }

    public PasswordVerification Verify(ReadOnlySpan<byte> passwordUtf8, string encoded)
    {
        if (passwordUtf8.IsEmpty || string.IsNullOrWhiteSpace(encoded))
        {
            PerformDummyWork(passwordUtf8);
            return PasswordVerification.Failed;
        }

        string[] parts = encoded.Split('$');
        if (parts.Length != 5 || parts[0] != "pbkdf2-sha512" ||
            !TryReadPositive(parts[1], "v=", out int version) ||
            !TryReadPositive(parts[2], "i=", out int iterations) ||
            iterations is < 100_000 or > 5_000_000)
        {
            PerformDummyWork(passwordUtf8);
            return PasswordVerification.Failed;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[3]);
            byte[] expected = Convert.FromBase64String(parts[4]);
            if (salt.Length is < 16 or > 64 || expected.Length != HashSize)
            {
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(salt);
                return PasswordVerification.Failed;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(passwordUtf8, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
            try
            {
                bool valid = CryptographicOperations.FixedTimeEquals(actual, expected);
                return valid
                    ? version == CurrentVersion && iterations == Iterations ? PasswordVerification.Valid : PasswordVerification.ValidNeedsRehash
                    : PasswordVerification.Failed;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(salt);
            }
        }
        catch (FormatException)
        {
            PerformDummyWork(passwordUtf8);
            return PasswordVerification.Failed;
        }
    }

    private static bool TryReadPositive(string value, string prefix, out int result)
    {
        result = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(value.AsSpan(prefix.Length), out result) && result > 0;
    }

    private static void ValidatePassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length is < 12 or > 256)
        {
            throw new ArgumentException("Passwords must contain between 12 and 256 characters.", nameof(password));
        }
    }

    private static void PerformDummyWork(string password)
    {
        Span<byte> salt = stackalloc byte[SaltSize];
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        CryptographicOperations.ZeroMemory(hash);
    }

    private static void PerformDummyWork(ReadOnlySpan<byte> passwordUtf8)
    {
        Span<byte> salt = stackalloc byte[SaltSize];
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(passwordUtf8, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        CryptographicOperations.ZeroMemory(hash);
    }
}

public enum PasswordVerification
{
    Failed,
    Valid,
    ValidNeedsRehash,
}
