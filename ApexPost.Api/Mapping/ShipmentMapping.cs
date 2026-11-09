using ApexPost.Api.Contracts;
using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;

namespace ApexPost.Api.Mapping;

/// <summary>Maps between request/response DTOs and the application records. No rules here, only copying.</summary>
public static class ShipmentMapping
{
    public static NewParcel ToNewParcel(this CreateShipmentRequest request) => new(
        request.TrackingNumber, request.WeightKg, request.RecipientName, request.RecipientPhone,
        request.RecipientAddress, request.RecipientCity);

    public static ParcelEdits ToEdits(this UpdateShipmentRequest request) => new(
        request.WeightKg, request.RecipientName, request.RecipientPhone, request.RecipientAddress, request.RecipientCity);

    public static ParcelFilter ToFilter(this ListShipmentsQuery query) => new(query.Page, query.PageSize, query.Status, query.City);

    public static IReadOnlyList<ScanInput> ToScanInputs(this ScanUploadRequest request) => request.Scans
        .Select(scan => new ScanInput(
            scan.ScanId, scan.ScanType ?? ParcelScanType.Pickup, scan.Location, scan.ScannedAtUtc ?? DateTimeOffset.MinValue,
            scan.Notes, scan.RecipientSignatureName))
        .ToList();

    public static ShipmentResponse ToResponse(this ParcelSummary parcel) => new(
        parcel.TrackingNumber, parcel.WeightKg, parcel.RecipientName, parcel.RecipientPhone, parcel.RecipientAddress,
        parcel.RecipientCity, (int)parcel.Status, parcel.CreatedAtUtc, parcel.UpdatedAtUtc);

    public static PagedResponse<ShipmentResponse> ToResponse(this ParcelPage page) =>
        new(page.Items.Select(item => item.ToResponse()).ToList(), page.Page, page.PageSize, page.TotalCount);

    public static ScanResponse ToResponse(this ParcelScanSummary scan) => new(
        scan.ScanId, (int)scan.ScanType, scan.CourierId, scan.Location, scan.ScannedAtUtc, scan.Notes, scan.RecipientSignatureName);

    public static ScanUploadResponse ToResponse(this ScanUploadResult result) => new(
        result.Accepted, result.DuplicatesSkipped,
        result.Rejected.Select(rejection => new ScanRejectionResponse(rejection.ScanId, rejection.Reason)).ToList());
}
