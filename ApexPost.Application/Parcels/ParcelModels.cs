using ApexPost.Domain.Parcels;

namespace ApexPost.Application.Parcels;

// Plain data shapes of the parcel use cases. Nothing here knows about HTTP, JSON or Entity Framework.

public sealed record ParcelScanSummary(
    string ScanId,
    ParcelScanType ScanType,
    string CourierId,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? RecipientSignatureName);

public sealed record ParcelSummary(
    string TrackingNumber,
    decimal WeightKg,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity,
    ParcelStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ParcelScanSummary? LatestScan);

public sealed record ParcelPage(IReadOnlyList<ParcelSummary> Items, int Page, int PageSize, int TotalCount);

public sealed record ParcelFilter(int Page, int PageSize, ParcelStatus? Status, string? City);

public sealed record NewParcel(
    string TrackingNumber,
    decimal WeightKg,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity);

public sealed record ParcelEdits(
    decimal WeightKg,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientCity);

public sealed record ScanInput(
    string ScanId,
    ParcelScanType ScanType,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? RecipientSignatureName);

public sealed record ScanRejection(string ScanId, string Reason);

/// <summary>accepted + duplicatesSkipped + rejected always add up to the number of scans that were sent.</summary>
public sealed record ScanUploadResult(int Accepted, int DuplicatesSkipped, IReadOnlyList<ScanRejection> Rejected);
