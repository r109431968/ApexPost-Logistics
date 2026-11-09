using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Contracts.V2;

/// <summary>Body of POST /api/v2/auth/token.</summary>
public sealed class TokenRequest
{
    /// <summary>The sign-in name.</summary>
    /// <example>courier007</example>
    [Required, StringLength(50, MinimumLength = 1)]
    public string UserName { get; init; } = "";

    /// <summary>The password.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Password { get; init; } = "";
}

/// <summary>The access token and how to use it.</summary>
/// <param name="AccessToken">Send it as: Authorization: Bearer {accessToken}.</param>
/// <param name="TokenType">Always Bearer.</param>
/// <param name="ExpiresInSeconds">How long the token is valid. After that, sign in again.</param>
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);

/// <summary>What a partner may know about a shipment. No recipient name, phone or address: a partner only needs the progress.</summary>
/// <param name="TrackingNumber">Public tracking number.</param>
/// <param name="Status">Created, PickedUp, InTransit, OutForDelivery, Delivered or Failed.</param>
/// <param name="UpdatedAtUtc">When the shipment last changed.</param>
/// <param name="LastScanLocation">Where the newest scan happened, or null when there is none yet.</param>
/// <param name="LastScanAtUtc">When the newest scan happened, or null when there is none yet.</param>
public sealed record PartnerShipmentResponse(
    string TrackingNumber,
    ApexPost.Domain.Parcels.ParcelStatus Status,
    DateTimeOffset UpdatedAtUtc,
    string? LastScanLocation,
    DateTimeOffset? LastScanAtUtc);
