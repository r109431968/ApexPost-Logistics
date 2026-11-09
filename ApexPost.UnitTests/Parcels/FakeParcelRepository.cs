using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;

namespace ApexPost.UnitTests.Parcels;

/// <summary>An in-memory parcel store, so the service rules can be tested without a database.</summary>
public sealed class FakeParcelRepository : IParcelRepository
{
    private readonly Dictionary<string, ParcelSummary> _parcels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Tracking, string Courier, ScanInput Scan)> _scans = [];

    public int StoredScanCount => _scans.Count;

    public Task<ParcelPage> ListAsync(ParcelFilter filter, CancellationToken cancellationToken)
    {
        var items = _parcels.Values
            .Where(p => filter.Status is null || p.Status == filter.Status)
            .Where(p => string.IsNullOrWhiteSpace(filter.City) || p.RecipientCity.Equals(filter.City, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.TrackingNumber)
            .ToList();

        var page = items.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult(new ParcelPage(page, filter.Page, filter.PageSize, items.Count));
    }

    public Task<ParcelSummary?> FindAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_parcels.GetValueOrDefault(trackingNumber));

    public Task<bool> ExistsAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_parcels.ContainsKey(trackingNumber));

    public Task AddAsync(NewParcel parcel, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _parcels[parcel.TrackingNumber] = new ParcelSummary(
            parcel.TrackingNumber, parcel.WeightKg, parcel.RecipientName, parcel.RecipientPhone, parcel.RecipientAddress,
            parcel.RecipientCity, ParcelStatus.Created, now, now, null);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceDetailsAsync(string trackingNumber, ParcelEdits edits, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!_parcels.TryGetValue(trackingNumber, out var current))
            return Task.FromResult(false);

        _parcels[trackingNumber] = current with
        {
            WeightKg = edits.WeightKg, RecipientName = edits.RecipientName, RecipientPhone = edits.RecipientPhone,
            RecipientAddress = edits.RecipientAddress, RecipientCity = edits.RecipientCity, UpdatedAtUtc = now
        };
        return Task.FromResult(true);
    }

    public Task<bool> SetStatusAsync(string trackingNumber, ParcelStatus expected, ParcelStatus status, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!_parcels.TryGetValue(trackingNumber, out var current))
            return Task.FromResult(false);
        if (current.Status != expected)
            throw new ConcurrencyConflictException();

        _parcels[trackingNumber] = current with { Status = status, UpdatedAtUtc = now };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        _scans.RemoveAll(scan => scan.Tracking == trackingNumber);
        return Task.FromResult(_parcels.Remove(trackingNumber));
    }

    public Task<IReadOnlyList<ParcelScanSummary>> ScansAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        IReadOnlyList<ParcelScanSummary> result = _scans
            .Where(scan => scan.Tracking == trackingNumber)
            .OrderBy(scan => scan.Scan.ScannedAtUtc)
            .Select(scan => new ParcelScanSummary(
                scan.Scan.ScanId, scan.Scan.ScanType, scan.Courier, scan.Scan.Location, scan.Scan.ScannedAtUtc,
                scan.Scan.Notes, scan.Scan.RecipientSignatureName))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<HashSet<string>> ExistingScanIdsAsync(IReadOnlyCollection<string> scanIds, CancellationToken cancellationToken) =>
        Task.FromResult(_scans.Select(scan => scan.Scan.ScanId).Where(scanIds.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase));

    public Task AddScansAsync(string trackingNumber, string courierId, IReadOnlyList<ScanInput> scans, DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        _scans.AddRange(scans.Select(scan => (trackingNumber, courierId, scan)));
        return Task.CompletedTask;
    }
}
