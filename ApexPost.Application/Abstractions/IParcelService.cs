using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;

namespace ApexPost.Application.Abstractions;

/// <summary>The parcel use cases behind /api/shipments. Controllers call these and nothing else.</summary>
public interface IParcelService
{
    Task<ParcelPage> ListAsync(ParcelFilter filter, CancellationToken cancellationToken);

    Task<ParcelSummary> GetAsync(string trackingNumber, CancellationToken cancellationToken);

    Task<ParcelSummary> CreateAsync(NewParcel parcel, CancellationToken cancellationToken);

    Task<ParcelSummary> ReplaceAsync(string trackingNumber, ParcelEdits edits, CancellationToken cancellationToken);

    Task<ParcelSummary> ChangeStatusAsync(string trackingNumber, ParcelStatus status, CancellationToken cancellationToken);

    Task DeleteAsync(string trackingNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<ParcelScanSummary>> ScansAsync(string trackingNumber, CancellationToken cancellationToken);

    Task<ScanUploadResult> UploadScansAsync(string trackingNumber, string courierId, IReadOnlyList<ScanInput> scans, CancellationToken cancellationToken);
}
