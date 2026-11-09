using Asp.Versioning;
using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Security;
using ApexPost.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V2;

/// <summary>Read-only tracking for partner systems (API key).</summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/partner/shipments")]
[Authorize(Policy = SecurityRegistration.PartnerPolicy)]
[Produces("application/json")]
[Tags("Partner")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class PartnerShipmentsController(IParcelService parcels) : ControllerBase
{
    /// <summary>Gets the progress of one shipment. Authenticated with the X-Api-Key header; a JWT is not accepted here.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The status and the newest scan, without personal data of the recipient.</returns>
    [HttpGet("{trackingNumber:regex(^APX-\\d{{8}}$)}", Name = "GetPartnerShipmentV2")]
    [EndpointSummary("Track a shipment (partner)")]
    [ProducesResponseType<PartnerShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PartnerShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken)
    {
        var parcel = await parcels.GetAsync(trackingNumber, cancellationToken);
        return Ok(new PartnerShipmentResponse(
            parcel.TrackingNumber,
            parcel.Status,
            parcel.UpdatedAtUtc,
            parcel.LatestScan?.Location,
            parcel.LatestScan?.ScannedAtUtc));
    }
}
