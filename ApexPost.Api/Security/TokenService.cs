using ApexPost.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAtUtc, int ExpiresInSeconds);

public interface ITokenService
{
    IssuedToken Issue(DemoUser user);
}

/// <summary>Creates short-lived signed access tokens (JWT, HMAC-SHA256) with the claims the API needs: sub, role and, for couriers, courierId.</summary>
public sealed class TokenService(JwtSettings settings, IClock clock) : ITokenService
{
    public IssuedToken Issue(DemoUser user)
    {
        var now = clock.UtcNow;
        var expires = now.Add(settings.Lifetime);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.UserName,
            ["role"] = user.Role
        };
        if (user.CourierId is not null)
            claims[ApiClaims.CourierId] = user.CourierId;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expires, (int)settings.Lifetime.TotalSeconds);
    }
}
