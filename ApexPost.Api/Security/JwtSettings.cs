using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

/// <summary>
/// Settings for signing and checking access tokens. The signing key is a secret: it comes from user-secrets or an environment
/// variable (Auth__Jwt__SigningKey), never from a committed file. Issuer, audience and lifetime are not secret and live in appsettings.json.
/// </summary>
public sealed class JwtSettings
{
    public const int MinimumKeyBytes = 32;

    public JwtSettings(string issuer, string audience, TimeSpan lifetime, byte[] keyBytes, bool keyIsEphemeral)
    {
        if (keyBytes.Length < MinimumKeyBytes)
            throw new InvalidOperationException($"The JWT signing key must be at least {MinimumKeyBytes} bytes long.");

        Issuer = issuer;
        Audience = audience;
        Lifetime = lifetime;
        SigningKey = new SymmetricSecurityKey(keyBytes);
        KeyIsEphemeral = keyIsEphemeral;
    }

    public string Issuer { get; }
    public string Audience { get; }
    public TimeSpan Lifetime { get; }
    public SymmetricSecurityKey SigningKey { get; }

    /// <summary>True when no key was configured and a random one was made for this run (Development only). Tokens stop working at restart.</summary>
    public bool KeyIsEphemeral { get; }

    public static JwtSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        var issuer = configuration["Auth:Jwt:Issuer"] ?? "apexpost-api";
        var audience = configuration["Auth:Jwt:Audience"] ?? "apexpost-clients";
        var minutes = int.TryParse(configuration["Auth:Jwt:LifetimeMinutes"], out var parsed) && parsed is > 0 and <= 1440 ? parsed : 15;

        var key = configuration["Auth:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Auth:Jwt:SigningKey is not set. Set it as a secret (environment variable Auth__Jwt__SigningKey or your secret store). It must be at least 32 characters.");
            }

            return new JwtSettings(issuer, audience, TimeSpan.FromMinutes(minutes), RandomNumberGenerator.GetBytes(MinimumKeyBytes), keyIsEphemeral: true);
        }

        return new JwtSettings(issuer, audience, TimeSpan.FromMinutes(minutes), Encoding.UTF8.GetBytes(key), keyIsEphemeral: false);
    }
}
