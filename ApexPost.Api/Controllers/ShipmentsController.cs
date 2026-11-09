using ApexPost.Api.Contracts;
using ApexPost.Api.Mapping;
using ApexPost.Application.Abstractions;
using ApexPost.Domain.Parcels;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers;

/// <summary>Create, read, change and delete shipments.</summary>
/// <remarks>
/// Controllers only bind the input, call <see cref="IParcelService"/> and return an HTTP result.
/// Every business rule (transitions, duplicates, delete rule) lives in the service.
/// </remarks>
[ApiController]
[Route("api/shipments")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Shipments")]
public sealed class ShipmentsController(IParcelService parcels) : ControllerBase
{
    private const string TrackingRoute = "{trackingNumber:regex(^APX-\\d{{8}}$)}";

    /// <summary>Lists shipments, one page at a time, optionally filtered by status and city.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>One page of shipments.</returns>
    [HttpGet(Name = "ListShipments")]
    [EndpointSummary("List shipments")]
    [ProducesResponseType<PagedResponse<ShipmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<ShipmentResponse>>> List([FromQuery] ListShipmentsQuery query, CancellationToken cancellationToken) =>
        Ok((await parcels.ListAsync(query.ToFilter(), cancellationToken)).ToResponse());

    /// <summary>Gets one shipment.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The shipment.</returns>
    [HttpGet(TrackingRoute, Name = "GetShipment")]
    [EndpointSummary("Get one shipment")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken) =>
        Ok((await parcels.GetAsync(trackingNumber, cancellationToken)).ToResponse());

    /// <summary>Creates a shipment in status Created.</summary>
    /// <param name="request">The shipment details.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The new shipment, with a Location header that points to it.</returns>
    [HttpPost(Name = "CreateShipment")]
    [EndpointSummary("Create a shipment")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponse>> Create(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var created = await parcels.CreateAsync(request.ToNewParcel(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { trackingNumber = created.TrackingNumber }, created.ToResponse());
    }

    /// <summary>Replaces the editable details of a shipment (not the tracking number, not the status).</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="request">The new details.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The updated shipment.</returns>
    [HttpPut(TrackingRoute, Name = "ReplaceShipment")]
    [EndpointSummary("Replace shipment details")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponse>> Replace(string trackingNumber, UpdateShipmentRequest request, CancellationToken cancellationToken) =>
        Ok((await parcels.ReplaceAsync(trackingNumber, request.ToEdits(), cancellationToken)).ToResponse());

    /// <summary>Moves a shipment to its next status: Created, PickedUp, InTransit, OutForDelivery, then Delivered or Failed.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="request">The new status.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <returns>The updated shipment.</returns>
    [HttpPatch(TrackingRoute + "/status", Name = "UpdateShipmentStatus")]
    [EndpointSummary("Change the status")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShipmentResponse>> UpdateStatus(string trackingNumber, UpdateStatusRequest request, CancellationToken cancellationToken) =>
        Ok((await parcels.ChangeStatusAsync(trackingNumber, request.Status!.Value, cancellationToken)).ToResponse());

    /// <summary>Deletes a shipment. Only allowed while its status is Created.</summary>
    /// <param name="trackingNumber">For example APX-12345678.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    [HttpDelete(TrackingRoute, Name = "DeleteShipment")]
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
