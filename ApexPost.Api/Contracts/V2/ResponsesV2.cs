using ApexPost.Domain.Parcels;

namespace ApexPost.Api.Contracts.V2;

/// <summary>Who receives the shipment. Grouped into one object in version 2.</summary>
/// <param name="Name">Recipient's name.</param>
/// <param name="Phone">Recipient's phone.</param>
/// <param name="Address">Recipient's street address.</param>
/// <param name="City">Recipient's city.</param>
public sealed record RecipientResponse(string Name, string Phone, string Address, string City);

/// <summary>The most recent scan of a shipment, in short.</summary>
/// <param name="ScanType">Pickup, InTransit, Delivered or Failed.</param>
/// <param name="Location">Where the scan happened.</param>
/// <param name="ScannedAtUtc">When the scan happened.</param>
public sealed record LatestScanResponse(ParcelScanType ScanType, string Location, DateTimeOffset ScannedAtUtc);

/// <summary>A shipment as returned by version 2 of the API: nested recipient, latest scan summary and a string status.</summary>
/// <param name="TrackingNumber">Public tracking number.</param>
/// <param name="WeightKg">Weight in kilograms.</param>
/// <param name="Recipient">Who receives the shipment.</param>
/// <param name="Status">Created, PickedUp, InTransit, OutForDelivery, Delivered or Failed.</param>
/// <param name="CreatedAtUtc">When the shipment was registered.</param>
/// <param name="UpdatedAtUtc">When the shipment last changed.</param>
/// <param name="LatestScan">The newest scan, or null when there is none yet.</param>
public sealed record ShipmentResponseV2(
    string TrackingNumber,
    decimal WeightKg,
    RecipientResponse Recipient,
    ParcelStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    LatestScanResponse? LatestScan);

/// <summary>A scan as returned by version 2 of the API (the scan type is a string).</summary>
/// <param name="ScanId">The id made on the courier's phone.</param>
/// <param name="ScanType">Pickup, InTransit, Delivered or Failed.</param>
/// <param name="CourierId">Who scanned.</param>
/// <param name="Location">Where.</param>
/// <param name="ScannedAtUtc">When.</param>
/// <param name="Notes">Free text.</param>
/// <param name="RecipientSignatureName">Who signed (Delivered scans).</param>
public sealed record ScanResponseV2(
    string ScanId,
    ParcelScanType ScanType,
    string CourierId,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? RecipientSignatureName);
