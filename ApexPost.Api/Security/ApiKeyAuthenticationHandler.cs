using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ApexPost.Api.Security;

/// <summary>
/// Authenticates a partner system by the X-Api-Key header. The key is a secret (Partner:ApiKey in user-secrets / an environment
/// variable), compared in constant time, and never written to a log. An API key identifies a system, not a person, and cannot
/// expire or carry scopes by itself, which is why it is only accepted on the partner route (see SECURITY.md).
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.Count != 1 || string.IsNullOrEmpty(values[0]))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expected = configuration["Partner:ApiKey"];
        if (string.IsNullOrEmpty(expected))
        {
            Logger.LogWarning("A partner request was refused because Partner:ApiKey is not configured.");
            return Task.FromResult(AuthenticateResult.Fail("Partner access is not configured."));
        }

        if (!DemoUserDirectory.SecretsMatch(values[0]!, expected))
        {
            Logger.LogWarning("A partner request was refused: the API key is wrong.");   // the key itself is never logged
            return Task.FromResult(AuthenticateResult.Fail("The API key is not valid."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "partner"), new Claim(ClaimTypes.Role, ApiRoles.Partner)],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"ApiKey header=\"{HeaderName}\"";
        return Task.CompletedTask;
    }
}
