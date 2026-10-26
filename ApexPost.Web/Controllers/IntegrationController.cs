using System.Text.RegularExpressions;
using ApexPost.Web.Composition;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

/// <summary>
/// Endpoints for courier partners. They do not use the login cookie: the middleware checks the IP allowlist,
/// then the API key, then the per-partner rate limit, before this code runs.
/// </summary>
[AllowAnonymous]
[IpRestricted(IpZone.Integration)]
[RequireApiKey]
[EnableRateLimiting(WebServiceRegistration.PartnerRateLimit)]
[EnableCors(WebServiceRegistration.PartnerCorsPolicy)]
[Route("integration")]
public sealed partial class IntegrationController(ICourierTrackingService courier) : ControllerBase
{
    [GeneratedRegex(@"^[A-Za-z0-9\-]{6,40}$")]
    private static partial Regex TrackingNumberPattern();

    [HttpGet("parcel-status/{trackingNumber}")]
    public async Task<IActionResult> ParcelStatus(string trackingNumber, CancellationToken cancellationToken)
    {
        if (!TrackingNumberPattern().IsMatch(trackingNumber))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid tracking number",
                Detail = "Use 6 to 40 letters, digits or dashes."
            });

        // A caller that disconnects cancels the token (handled quietly by the exception middleware).
        // A slow courier hits the service's own timeout and becomes a 504. A broken courier becomes a 502.
        var status = await courier.GetStatusAsync(trackingNumber, cancellationToken);
        return Ok(status);
    }
}
