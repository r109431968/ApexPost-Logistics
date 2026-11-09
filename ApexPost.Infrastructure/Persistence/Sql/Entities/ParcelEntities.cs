using ApexPost.Domain.Parcels;

namespace ApexPost.Infrastructure.Persistence.Sql.Entities;

// Persistence shapes of the Week 4 shipment API. Plain data, no business rules (those are in ParcelService / ParcelTransitions).

public sealed class ParcelEntity
{
    public string TrackingNumber { get; set; } = null!;
    public decimal WeightKg { get; set; }
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public string RecipientAddress { get; set; } = null!;
    public string RecipientCity { get; set; } = null!;
    public ParcelStatus Status { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime LastChangedAtUtc { get; set; }
    public List<ParcelScanEntity> Scans { get; set; } = [];
}

public sealed class ParcelScanEntity
{
    public string ScanId { get; set; } = null!;
    public string TrackingNumber { get; set; } = null!;
    public ParcelScanType ScanType { get; set; }
    public string CourierId { get; set; } = null!;
    public string Location { get; set; } = null!;
    public DateTime ScannedAtUtc { get; set; }
    public string? Notes { get; set; }
    public string? RecipientSignatureName { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public ParcelEntity? Parcel { get; set; }
}
