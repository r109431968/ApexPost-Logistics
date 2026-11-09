using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Parcels;
using ApexPost.Domain.Parcels;
using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// Reads are AsNoTracking projections into plain records. Writes load the row tracked and only stage the change;
/// the unit of work commits. No IQueryable leaves this class.
/// </summary>
public sealed class ParcelRepository(ApexPostDbContext db) : IParcelRepository
{
    public async Task<ParcelPage> ListAsync(ParcelFilter filter, CancellationToken cancellationToken)
    {
        IQueryable<ParcelEntity> query = db.Parcels.AsNoTracking();

        if (filter.Status is { } status)
            query = query.Where(parcel => parcel.Status == status);

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();
            query = query.Where(parcel => parcel.RecipientCity == city);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(parcel => parcel.TrackingNumber)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(parcel => new
            {
                Parcel = parcel,
                Latest = parcel.Scans.OrderByDescending(scan => scan.ScannedAtUtc).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new ParcelPage(rows.Select(row => ToSummary(row.Parcel, row.Latest)).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<ParcelSummary?> FindAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        var row = await db.Parcels.AsNoTracking()
            .Where(parcel => parcel.TrackingNumber == trackingNumber)
            .Select(parcel => new
            {
                Parcel = parcel,
                Latest = parcel.Scans.OrderByDescending(scan => scan.ScannedAtUtc).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToSummary(row.Parcel, row.Latest);
    }

    public Task<bool> ExistsAsync(string trackingNumber, CancellationToken cancellationToken) =>
        db.Parcels.AsNoTracking().AnyAsync(parcel => parcel.TrackingNumber == trackingNumber, cancellationToken);

    public Task AddAsync(NewParcel parcel, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.Parcels.Add(new ParcelEntity
        {
            TrackingNumber = parcel.TrackingNumber,
            WeightKg = parcel.WeightKg,
            RecipientName = parcel.RecipientName,
            RecipientPhone = parcel.RecipientPhone,
            RecipientAddress = parcel.RecipientAddress,
            RecipientCity = parcel.RecipientCity,
            Status = ParcelStatus.Created,
            RegisteredAtUtc = now.UtcDateTime,
            LastChangedAtUtc = now.UtcDateTime
        });

        return Task.CompletedTask;
    }

    public async Task<bool> ReplaceDetailsAsync(string trackingNumber, ParcelEdits edits, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await db.Parcels.FirstOrDefaultAsync(parcel => parcel.TrackingNumber == trackingNumber, cancellationToken);
        if (row is null)
            return false;

        row.WeightKg = edits.WeightKg;
        row.RecipientName = edits.RecipientName;
        row.RecipientPhone = edits.RecipientPhone;
        row.RecipientAddress = edits.RecipientAddress;
        row.RecipientCity = edits.RecipientCity;
        row.LastChangedAtUtc = now.UtcDateTime;
        return true;
    }

    public async Task<bool> SetStatusAsync(
        string trackingNumber, ParcelStatus expected, ParcelStatus status, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await db.Parcels.FirstOrDefaultAsync(parcel => parcel.TrackingNumber == trackingNumber, cancellationToken);
        if (row is null)
            return false;

        if (row.Status != expected)
            throw new ConcurrencyConflictException();

        row.Status = status;
        row.LastChangedAtUtc = now.UtcDateTime;
        return true;
    }

    public async Task<bool> DeleteAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        var row = await db.Parcels.FirstOrDefaultAsync(parcel => parcel.TrackingNumber == trackingNumber, cancellationToken);
        if (row is null)
            return false;

        db.Parcels.Remove(row);
        return true;
    }

    public async Task<IReadOnlyList<ParcelScanSummary>> ScansAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        var rows = await db.ParcelScans.AsNoTracking()
            .Where(scan => scan.TrackingNumber == trackingNumber)
            .OrderBy(scan => scan.ScannedAtUtc).ThenBy(scan => scan.ScanId)
            .ToListAsync(cancellationToken);

        return rows.Select(ToSummary).ToList();
    }

    public async Task<HashSet<string>> ExistingScanIdsAsync(IReadOnlyCollection<string> scanIds, CancellationToken cancellationToken)
    {
        var ids = scanIds.ToList();

        var found = await db.ParcelScans.AsNoTracking()
            .Where(scan => ids.Contains(scan.ScanId))
            .Select(scan => scan.ScanId)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(found, StringComparer.OrdinalIgnoreCase);
    }

    public Task AddScansAsync(
        string trackingNumber, string courierId, IReadOnlyList<ScanInput> scans, DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        db.ParcelScans.AddRange(scans.Select(scan => new ParcelScanEntity
        {
            ScanId = scan.ScanId,
            TrackingNumber = trackingNumber,
            ScanType = scan.ScanType,
            CourierId = courierId,
            Location = scan.Location,
            ScannedAtUtc = scan.ScannedAtUtc.UtcDateTime,
            Notes = scan.Notes,
            RecipientSignatureName = scan.RecipientSignatureName,
            ReceivedAtUtc = receivedAt.UtcDateTime
        }));

        return Task.CompletedTask;
    }

    private static ParcelScanSummary ToSummary(ParcelScanEntity scan) => new(
        scan.ScanId, scan.ScanType, scan.CourierId, scan.Location,
        new DateTimeOffset(DateTime.SpecifyKind(scan.ScannedAtUtc, DateTimeKind.Utc)),
        scan.Notes, scan.RecipientSignatureName);

    private static ParcelSummary ToSummary(ParcelEntity parcel, ParcelScanEntity? latest) => new(
        parcel.TrackingNumber, parcel.WeightKg, parcel.RecipientName, parcel.RecipientPhone, parcel.RecipientAddress,
        parcel.RecipientCity, parcel.Status,
        new DateTimeOffset(DateTime.SpecifyKind(parcel.RegisteredAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(parcel.LastChangedAtUtc, DateTimeKind.Utc)),
        latest is null ? null : ToSummary(latest));
}
