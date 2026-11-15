namespace ApexPost.Courier.Offline.Domain;

/// <summary>
/// One scan of a shipment by a courier. This is a plain domain record: it knows nothing about Couchbase.
/// </summary>
public sealed record ShipmentScan
{
    public const string IdPrefix = "scan::";

    public required string Id { get; init; }
    public required string TrackingNumber { get; init; }
    public required ScanType ScanType { get; init; }
    public required string CourierId { get; init; }
    public required string Location { get; init; }
    public required DateTimeOffset ScannedAtUtc { get; init; }
    public string? Notes { get; init; }

    /// <summary>Name of the person who signed for the parcel. The central system requires it for a Delivered scan.</summary>
    public string? RecipientSignatureName { get; init; }

    public SyncStatus SyncStatus { get; init; } = SyncStatus.Pending;

    /// <summary>Creates a new scan that is waiting to be uploaded (syncStatus = Pending).</summary>
    public static ShipmentScan CreatePending(
        string trackingNumber,
        ScanType scanType,
        string courierId,
        string location,
        string? notes = null,
        DateTimeOffset? scannedAtUtc = null,
        string? recipientSignatureName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(courierId);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (!Enum.IsDefined(scanType))
        {
            throw new ArgumentOutOfRangeException(nameof(scanType), scanType, "Unknown scan type.");
        }

        return new ShipmentScan
        {
            Id = NewId(),
            TrackingNumber = trackingNumber.Trim(),
            ScanType = scanType,
            CourierId = courierId.Trim(),
            Location = location.Trim(),
            ScannedAtUtc = TruncateToMilliseconds((scannedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime()),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RecipientSignatureName = string.IsNullOrWhiteSpace(recipientSignatureName) ? null : recipientSignatureName.Trim(),
            SyncStatus = SyncStatus.Pending
        };
    }

    /// <summary>Document ids look like scan::{Guid}.</summary>
    public static string NewId() => IdPrefix + Guid.NewGuid();

    public static bool IsValidId(string? id) =>
        id is not null
        && id.StartsWith(IdPrefix, StringComparison.Ordinal)
        && Guid.TryParse(id.AsSpan(IdPrefix.Length), out _);

    public ShipmentScan MarkSynced() => this with { SyncStatus = SyncStatus.Synced };

    public ShipmentScan MarkFailed() => this with { SyncStatus = SyncStatus.Failed };

    // The store keeps times with millisecond precision, so the domain does the same. Then a scan read back is equal to the one saved.
    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
}
