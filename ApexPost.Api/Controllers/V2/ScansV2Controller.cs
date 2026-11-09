using Asp.Versioning;
using ApexPost.Api.Contracts;
using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Mapping;
using ApexPost.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V2;

/// <summary>Scan history and the bulk upload of scans recorded offline (version 2).</summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/shipments/{trackingNumber:regex(^APX-\\d{{8}}$)}/scans")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Scans")]
public sealed class ScansV2Controller(IParcelService parcels) : ControllerBase
{
    /// <summary>Uploads a batch of scans from the courier's offline store. Safe to repeat: scans that are already stored are skipped.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="request">The courier and the scans.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>How many scans were accepted, skipped as duplicates and rejected (with reasons).</returns>
    [HttpPost(Name = "UploadScansV2")]
    [EndpointSummary("Upload offline scans")]
    [ProducesResponseType<ScanUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScanUploadResponse>> Upload(string trackingNumber, ScanUploadRequest request, CancellationToken cancellationToken) =>
        Ok((await parcels.UploadScansAsync(trackingNumber, request.CourierId, request.ToScanInputs(), cancellationToken)).ToResponse());

    /// <summary>Lists the scans of a shipment, oldest first.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The scan history.</returns>
    [HttpGet(Name = "ListScansV2")]
    [EndpointSummary("Get the scan history")]
    [ProducesResponseType<IReadOnlyList<ScanResponseV2>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ScanResponseV2>>> List(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await parcels.ScansAsync(trackingNumber, cancellationToken)).Select(scan => scan.ToResponseV2()).ToList());
}
