using System.Text;
using System.Text.Json;

namespace ApexPost.Courier.Core.Auth;

/// <param name="UserName">The sub claim.</param>
/// <param name="Role">Courier or Admin.</param>
/// <param name="CourierId">The courierId claim; null for an Admin.</param>
/// <param name="ExpiresAtUtc">When the token stops being valid.</param>
public sealed record TokenInfo(string? UserName, string? Role, string? CourierId, DateTimeOffset? ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc is { } expires && expires <= now;
}

/// <summary>
/// Reads the claims of a JWT to show the user name and to know the courier id. This does NOT check the signature and must never be
/// used to decide what is allowed: the server checks the token on every call. The app only needs the values to display and to fill in forms.
/// </summary>
public static class JwtClaimReader
{
    public static TokenInfo? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            DateTimeOffset? expires = root.TryGetProperty("exp", out var exp) && exp.ValueKind == JsonValueKind.Number && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;

            return new TokenInfo(Text(root, "sub"), Text(root, "role"), Text(root, "courierId"), expires);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
