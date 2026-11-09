namespace ApexPost.Api.Contracts;

/// <summary>One page of results.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="TotalCount">Items on all pages together.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>A shipment as returned by version 1 of the API (flat recipient fields, numeric status).</summary>
/// <param name="TrackingNumber">Public tracking number.</param>
/// <param name="WeightKg">Weight in kilograms.</param>
/// <param name="RecipientName">Recipient's name.</param>
/// <param name="RecipientPhone">Recipient's phone.</param>
/// <param name="RecipientAddress">Recipient's street address.</param>
/// <param name="RecipientCity">Recipient's city.</param>
/// <param name="Status">0 Created, 1 PickedUp, 2 InTransit, 3 OutForDelivery, 4 Delivered, 5 Failed.</param>
/// <param name="CreatedAtUtc">When the shipment was registered.</param>
/// <param name="UpdatedAtUtc">When the shipment last changed.</param>
public sealed record ShipmentResponse(
    string TrackingNumber,
    decimal WeightKg,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity,
    int Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>A scan as returned by version 1 of the API.</summary>
/// <param name="ScanId">The id made on the courier's phone.</param>
/// <param name="ScanType">0 Pickup, 1 InTransit, 2 Delivered, 3 Failed.</param>
/// <param name="CourierId">Who scanned.</param>
/// <param name="Location">Where.</param>
/// <param name="ScannedAtUtc">When.</param>
/// <param name="Notes">Free text.</param>
/// <param name="RecipientSignatureName">Who signed (Delivered scans).</param>
public sealed record ScanResponse(
    string ScanId,
    int ScanType,
    string CourierId,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? RecipientSignatureName);

/// <summary>Why one scan of a batch was not stored.</summary>
/// <param name="ScanId">The scan that was refused.</param>
/// <param name="Reason">The reason, in words.</param>
public sealed record ScanRejectionResponse(string ScanId, string Reason);

/// <summary>Result of a bulk scan upload. Accepted + DuplicatesSkipped + Rejected.Count = scans sent.</summary>
/// <param name="Accepted">Scans stored by this request.</param>
/// <param name="DuplicatesSkipped">Scans that were already stored, so nothing was added.</param>
/// <param name="Rejected">Scans refused, with the reason for each.</param>
public sealed record ScanUploadResponse(int Accepted, int DuplicatesSkipped, IReadOnlyList<ScanRejectionResponse> Rejected);
