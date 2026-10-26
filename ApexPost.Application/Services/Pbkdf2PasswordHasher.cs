using System.Security.Cryptography;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;

namespace ApexPost.Application.Services;

/// <summary>Passwords are stored as PBKDF2-HMAC-SHA256 with a random salt. Format: pbkdf2-sha256$iterations$salt$hash.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Scheme = "pbkdf2-sha256";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly PasswordSettings _settings;
    private readonly Lazy<string> _dummy;

    public Pbkdf2PasswordHasher(PasswordSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _dummy = new Lazy<string>(() => Hash("apexpost-dummy-password"));
    }

    public string DummyHash => _dummy.Value;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _settings.Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Scheme}${_settings.Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedHash);

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations) || iterations < 1)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
