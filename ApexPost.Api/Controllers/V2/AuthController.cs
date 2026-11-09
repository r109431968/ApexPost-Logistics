using Asp.Versioning;
using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Api.Controllers.V2;

/// <summary>Sign in and get an access token.</summary>
/// <remarks>
/// A stand-in for an identity provider so the API can run alone. In production the token comes from a real OAuth 2.0 / OpenID Connect
/// provider and the API only checks it (see SECURITY.md).
/// </remarks>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthController(IDemoUserDirectory users, ITokenService tokens) : ControllerBase
{
    /// <summary>Checks the user name and the password and returns a short-lived access token.</summary>
    /// <param name="request">The user name and the password.</param>
    /// <returns>The access token. Send it as <c>Authorization: Bearer {accessToken}</c>.</returns>
    [HttpPost("token", Name = "CreateTokenV2")]
    [EnableRateLimiting(SecurityRegistration.TokenRateLimit)]
    [EndpointSummary("Sign in and get an access token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public ActionResult<TokenResponse> CreateToken(TokenRequest request)
    {
        var user = users.Validate(request.UserName, request.Password);
        if (user is null)
        {
            // One message for a wrong name and a wrong password: the answer must not tell which names exist.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed", detail: "The user name or the password is wrong.");
        }

        var token = tokens.Issue(user);
        return Ok(new TokenResponse(token.AccessToken, "Bearer", token.ExpiresInSeconds));
    }
}
