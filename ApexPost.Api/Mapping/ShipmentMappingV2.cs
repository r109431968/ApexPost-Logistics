using ApexPost.Api.Contracts;
using ApexPost.Api.Contracts.V2;
using ApexPost.Application.Parcels;

namespace ApexPost.Api.Mapping;

/// <summary>Version 2 response shapes. Version 1 and version 2 call the same services; only the mapping differs.</summary>
public static class ShipmentMappingV2
{
    public static ShipmentResponseV2 ToResponseV2(this ParcelSummary parcel) => new(
        parcel.TrackingNumber,
        parcel.WeightKg,
        new RecipientResponse(parcel.RecipientName, parcel.RecipientPhone, parcel.RecipientAddress, parcel.RecipientCity),
        parcel.Status,
        parcel.CreatedAtUtc,
        parcel.UpdatedAtUtc,
        parcel.LatestScan is { } latest ? new LatestScanResponse(latest.ScanType, latest.Location, latest.ScannedAtUtc) : null);

    public static PagedResponse<ShipmentResponseV2> ToResponseV2(this ParcelPage page) =>
        new(page.Items.Select(item => item.ToResponseV2()).ToList(), page.Page, page.PageSize, page.TotalCount);

    public static ScanResponseV2 ToResponseV2(this ParcelScanSummary scan) => new(
        scan.ScanId, scan.ScanType, scan.CourierId, scan.Location, scan.ScannedAtUtc, scan.Notes, scan.RecipientSignatureName);
}
