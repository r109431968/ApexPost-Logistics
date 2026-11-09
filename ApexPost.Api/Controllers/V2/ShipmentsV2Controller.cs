using Asp.Versioning;
using ApexPost.Api.Contracts;
using ApexPost.Api.Security;
using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Mapping;
using ApexPost.Application.Abstractions;
using ApexPost.Domain.Parcels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V2;

/// <summary>Create, read, change and delete shipments (version 2).</summary>
/// <remarks>
/// Controllers only bind the input, call <see cref="IParcelService"/> and return an HTTP result.
/// Every business rule (transitions, duplicates, delete rule) lives in the service.
/// </remarks>
[ApiController]
[Authorize(Roles = ApiRoles.CourierOrAdmin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/shipments")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Shipments")]
public sealed class ShipmentsV2Controller(IParcelService parcels) : ControllerBase
{
    private const string TrackingRoute = "{trackingNumber:regex(^APX-\\d{{8}}$)}";

    /// <summary>Lists shipments, one page at a time, optionally filtered by status and city.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>One page of shipments.</returns>
    [HttpGet(Name = "ListShipmentsV2")]
    [EndpointSummary("List shipments")]
    [ProducesResponseType<PagedResponse<ShipmentResponseV2>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<ShipmentResponseV2>>> List([FromQuery] ListShipmentsQuery query, CancellationToken cancellationToken) =>
        Ok((await parcels.ListAsync(query.ToFilter(), cancellationToken)).ToResponseV2());

    /// <summary>Gets one shipment, with the recipient grouped and the latest scan.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The shipment.</returns>
    [HttpGet(TrackingRoute, Name = "GetShipmentV2")]
    [EndpointSummary("Get one shipment")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponseV2>> Get(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await parcels.GetAsync(trackingNumber, cancellationToken)).ToResponseV2());

    /// <summary>Creates a shipment in status Created.</summary>
    /// <param name="request">The shipment details.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The new shipment, with a Location header that points to it.</returns>
    [Authorize(Roles = ApiRoles.Admin)]
    [HttpPost(Name = "CreateShipmentV2")]
    [EndpointSummary("Create a shipment")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponseV2>> Create(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = await parcels.CreateAsync(request.ToNewParcel(), cancellationToken);
        return CreatedAtRoute("GetShipmentV2", new { version = "2", trackingNumber = created.TrackingNumber }, created.ToResponseV2());
    }

    /// <summary>Replaces the editable details of a shipment (not the tracking number, not the status).</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="request">The new details.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The updated shipment.</returns>
    [Authorize(Roles = ApiRoles.Admin)]
    [HttpPut(TrackingRoute, Name = "ReplaceShipmentV2")]
    [EndpointSummary("Replace shipment details")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponseV2>> Replace(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken) =>
        Ok((await parcels.ReplaceAsync(trackingNumber, request.ToEdits(), cancellationToken)).ToResponseV2());

    /// <summary>Moves a shipment to its next status: Created, PickedUp, InTransit, OutForDelivery, then Delivered or Failed.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="request">The new status.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The updated shipment.</returns>
    [HttpPatch(TrackingRoute + "/status", Name = "UpdateShipmentStatusV2")]
    [EndpointSummary("Change the status")]
    [ProducesResponseType<ShipmentResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponseV2>> UpdateStatus(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken) =>
        Ok((await parcels.ChangeStatusAsync(trackingNumber, request.Status!.Value, cancellationToken)).ToResponseV2());

    /// <summary>Deletes a shipment. Only allowed while its status is Created.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    [Authorize(Roles = ApiRoles.Admin)]
    [HttpDelete(TrackingRoute, Name = "DeleteShipmentV2")]
    [EndpointSummary("Delete a shipment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(string trackingNumber, CancellationToken cancellationToken)
    {
        await parcels.DeleteAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
