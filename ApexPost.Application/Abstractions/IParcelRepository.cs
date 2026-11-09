using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Storage of parcels and their scans. Like the other repositories, the write methods only stage changes;
/// <see cref="IUnitOfWork.SaveChangesAsync"/> commits them. Reads return plain records, never tracked entities.
/// </summary>
public interface IParcelRepository
{
    Task<ParcelPage> ListAsync(ParcelFilter filter, CancellationToken cancellationToken);

    Task<ParcelSummary?> FindAsync(string trackingNumber, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string trackingNumber, CancellationToken cancellationToken);

    Task AddAsync(NewParcel parcel, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Returns false when the tracking number is unknown.</summary>
    Task<bool> ReplaceDetailsAsync(string trackingNumber, ParcelEdits edits, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the status only if it is still <paramref name="expected"/>; otherwise somebody else changed it first and
    /// a <see cref="Exceptions.ConcurrencyConflictException"/> is thrown. Returns false when the tracking number is unknown.
    /// </summary>
    Task<bool> SetStatusAsync(string trackingNumber, ParcelStatus expected, ParcelStatus status, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Returns false when the tracking number is unknown.</summary>
    Task<bool> DeleteAsync(string trackingNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<ParcelScanSummary>> ScansAsync(string trackingNumber, CancellationToken cancellationToken);

    /// <summary>Which of these scan ids are already stored (for any parcel)?</summary>
    Task<HashSet<string>> ExistingScanIdsAsync(IReadOnlyCollection<string> scanIds, CancellationToken cancellationToken);

    Task AddScansAsync(string trackingNumber, string courierId, IReadOnlyList<ScanInput> scans, DateTimeOffset receivedAt, CancellationToken cancellationToken);
}
