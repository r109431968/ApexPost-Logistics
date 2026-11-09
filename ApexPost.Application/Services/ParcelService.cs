using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;

namespace ApexPost.Application.Services;

/// <summary>
/// All parcel business rules live here, so the controllers stay thin and every version of the API behaves the same.
/// One use case = one SaveChanges.
/// </summary>
public sealed class ParcelService(IParcelRepository parcels, IUnitOfWork unitOfWork, IClock clock) : IParcelService
{
    // A phone clock can be a few minutes ahead of the server. Anything further in the future is rejected.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public Task<ParcelPage> ListAsync(ParcelFilter filter, CancellationToken cancellationToken) =>
        parcels.ListAsync(filter, cancellationToken);

    public async Task<ParcelSummary> GetAsync(string trackingNumber, CancellationToken cancellationToken) =>
        await parcels.FindAsync(trackingNumber, cancellationToken)
            ?? throw new NotFoundException("Shipment", trackingNumber);

    public async Task<ParcelSummary> CreateAsync(NewParcel parcel, CancellationToken cancellationToken)
    {
        if (await parcels.ExistsAsync(parcel.TrackingNumber, cancellationToken))
            throw new ParcelConflictException($"A shipment with tracking number '{parcel.TrackingNumber}' already exists.");

        await parcels.AddAsync(Clean(parcel), clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(parcel.TrackingNumber, cancellationToken);
    }

    public async Task<ParcelSummary> ReplaceAsync(string trackingNumber, ParcelEdits edits, CancellationToken cancellationToken)
    {
        var current = await GetAsync(trackingNumber, cancellationToken);
        if (ParcelTransitions.IsFinal(current.Status))
            throw new ParcelConflictException($"Shipment '{trackingNumber}' is {current.Status} and can no longer be edited.");

        await parcels.ReplaceDetailsAsync(trackingNumber, Clean(edits), clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(trackingNumber, cancellationToken);
    }

    public async Task<ParcelSummary> ChangeStatusAsync(string trackingNumber, ParcelStatus status, CancellationToken cancellationToken)
    {
        var current = await GetAsync(trackingNumber, cancellationToken);
        if (!ParcelTransitions.CanMove(current.Status, status))
            throw new ParcelConflictException($"A shipment cannot move from {current.Status} to {status}.");

        await parcels.SetStatusAsync(trackingNumber, current.Status, status, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(trackingNumber, cancellationToken);
    }

    public async Task DeleteAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        var current = await GetAsync(trackingNumber, cancellationToken);
        if (current.Status != ParcelStatus.Created)
            throw new ParcelConflictException($"Only a shipment in status Created can be deleted, but '{trackingNumber}' is {current.Status}.");

        await parcels.DeleteAsync(trackingNumber, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParcelScanSummary>> ScansAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        if (!await parcels.ExistsAsync(trackingNumber, cancellationToken))
            throw new NotFoundException("Shipment", trackingNumber);

        return await parcels.ScansAsync(trackingNumber, cancellationToken);
    }

    /// <summary>
    /// Idempotent bulk upload. Every scan carries the id it got on the courier's phone, and that id is the primary key
    /// of the scan table. Sending the same batch twice (a retry after a dropped connection) therefore stores nothing new:
    /// the second answer just reports the scans as duplicatesSkipped.
    /// </summary>
    public async Task<ScanUploadResult> UploadScansAsync(
        string trackingNumber, string courierId, IReadOnlyList<ScanInput> scans, CancellationToken cancellationToken)
    {
        var parcel = await GetAsync(trackingNumber, cancellationToken);
        var now = clock.UtcNow;

        var alreadyStored = await parcels.ExistingScanIdsAsync(scans.Select(scan => scan.ScanId).Distinct().ToList(), cancellationToken);
        var seenInThisBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var accepted = new List<ScanInput>();
        var rejected = new List<ScanRejection>();
        var duplicates = 0;
        var status = parcel.Status;

        foreach (var scan in scans.OrderBy(scan => scan.ScannedAtUtc))
        {
            if (alreadyStored.Contains(scan.ScanId) || !seenInThisBatch.Add(scan.ScanId))
            {
                duplicates++;
                continue;
            }

            if (scan.ScannedAtUtc > now + ClockSkew)
            {
                rejected.Add(new ScanRejection(scan.ScanId, "The scan time is in the future."));
                continue;
            }

            if (ParcelTransitions.IsFinal(status))
            {
                rejected.Add(new ScanRejection(scan.ScanId, $"The shipment is already {status}, so no more scans are accepted."));
                continue;
            }

            accepted.Add(scan);

            if (ParcelTransitions.StatusAfterScan(status, scan.ScanType) is { } next)
                status = next;
        }

        if (accepted.Count > 0)
        {
            await parcels.AddScansAsync(trackingNumber, courierId, accepted, now, cancellationToken);

            if (status != parcel.Status)
                await parcels.SetStatusAsync(trackingNumber, parcel.Status, status, now, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ScanUploadResult(accepted.Count, duplicates, rejected);
    }

    private static NewParcel Clean(NewParcel parcel) => parcel with
    {
        TrackingNumber = parcel.TrackingNumber.Trim().ToUpperInvariant(),
        RecipientName = parcel.RecipientName.Trim(),
        RecipientPhone = parcel.RecipientPhone.Trim(),
        RecipientAddress = parcel.RecipientAddress.Trim(),
        RecipientCity = parcel.RecipientCity.Trim()
    };

    private static ParcelEdits Clean(ParcelEdits edits) => edits with
    {
        RecipientName = edits.RecipientName.Trim(),
        RecipientPhone = edits.RecipientPhone.Trim(),
        RecipientAddress = edits.RecipientAddress.Trim(),
        RecipientCity = edits.RecipientCity.Trim()
    };
}
