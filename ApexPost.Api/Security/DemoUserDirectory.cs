using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Api.Security;

/// <summary>A fictional user of the API.</summary>
/// <param name="UserName">Sign-in name.</param>
/// <param name="Role">Courier or Admin.</param>
/// <param name="CourierId">The courier id of a Courier; null for an Admin.</param>
public sealed record DemoUser(string UserName, string Role, string? CourierId);

public interface IDemoUserDirectory
{
    /// <summary>Returns the user when the name and the password are right; otherwise null (the caller must not say which one was wrong).</summary>
    DemoUser? Validate(string userName, string password);
}

/// <summary>
/// Stand-in for an identity provider, so the API can be run and tested alone. It knows three fictional users that share one demo
/// password. The password is a secret (Auth:DemoPassword in user-secrets), not a value in the repository. In production this class is
/// replaced by a real OAuth 2.0 / OpenID Connect provider and the API only validates the tokens (see SECURITY.md).
/// </summary>
public sealed class DemoUserDirectory(IConfiguration configuration) : IDemoUserDirectory
{
    private static readonly DemoUser[] Users =
    [
        new("courier007", ApiRoles.Courier, "courier-007"),
        new("courier008", ApiRoles.Courier, "courier-008"),
        new("admin", ApiRoles.Admin, null)
    ];

    public DemoUser? Validate(string userName, string password)
    {
        var configured = configuration["Auth:DemoPassword"];
        if (string.IsNullOrEmpty(configured))
            return null;   // not configured: nobody can sign in

        var user = Users.FirstOrDefault(candidate => string.Equals(candidate.UserName, userName, StringComparison.OrdinalIgnoreCase));

        // The password is always compared, even for an unknown name, so the time taken does not reveal which names exist.
        var passwordMatches = SecretsMatch(password, configured);
        return user is not null && passwordMatches ? user : null;
    }

    /// <summary>Compares two secrets in constant time (hashes of equal length, so neither the content nor the length leaks).</summary>
    public static bool SecretsMatch(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
