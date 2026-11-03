using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using Couchbase.Lite;
using Couchbase.Lite.Query;

namespace ApexPost.Courier.Offline.Storage;

/// <summary>
/// Couchbase Lite implementation of <see cref="IScanRepository"/>.
/// Key-value calls (GetDocument, Save, Delete) are used when the id is known; SQL++ queries are used for everything else.
/// Every query uses named parameters ($name). No user value is ever added to the query text.
/// </summary>
public sealed class CouchbaseScanRepository : IScanRepository
{
    private readonly CourierDatabase _database;

    public CouchbaseScanRepository(CourierDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    private Collection Collection => _database.Collection;

    public ShipmentScan Create(ShipmentScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        if (!ShipmentScan.IsValidId(scan.Id))
        {
            throw new ArgumentException("The scan id must have the format scan::{Guid}.", nameof(scan));
        }

        using (var existing = Collection.GetDocument(scan.Id))
        {
            if (existing is not null)
            {
                throw new InvalidOperationException($"A scan with id '{scan.Id}' already exists.");
            }
        }

        // Offline-first rule: a new scan is saved locally first and waits for the outbox, so it always starts as Pending.
        var pending = scan with { SyncStatus = SyncStatus.Pending };

        var document = new MutableDocument(pending.Id);
        ScanDocumentMapper.Write(document, pending);
        Collection.Save(document);
        return pending;
    }

    public ShipmentScan? GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var document = Collection.GetDocument(id);
        return document is null ? null : ScanDocumentMapper.Read(document);
    }

    public bool Update(ShipmentScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);

        using var existing = Collection.GetDocument(scan.Id);
        if (existing is null)
        {
            return false;
        }

        var document = existing.ToMutable();
        ScanDocumentMapper.Write(document, scan);
        Collection.Save(document);   // last write wins on this device: only this process writes the file
        return true;
    }

    public bool Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var document = Collection.GetDocument(id);
        if (document is null)
        {
            return false;
        }

        Collection.Delete(document);
        return true;
    }

    public IReadOnlyList<ShipmentScan> ListAll() => RunScanQuery(
        """
        SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus
        FROM _
        ORDER BY scannedAtUtc
        """);

    public IReadOnlyList<ShipmentScan> FindByTrackingNumber(string trackingNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

        return RunScanQuery(
            """
            SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus
            FROM _
            WHERE trackingNumber = $trackingNumber
            ORDER BY scannedAtUtc
            """,
            new Parameters().SetString("trackingNumber", trackingNumber.Trim()));
    }

    public IReadOnlyList<ShipmentScan> ListPending() => RunScanQuery(
        """
        SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus
        FROM _
        WHERE syncStatus = $syncStatus
        ORDER BY scannedAtUtc
        """,
        new Parameters().SetString("syncStatus", nameof(SyncStatus.Pending)));

    public int CountPending()
    {
        using var query = Collection.CreateQuery(
            """
            SELECT COUNT(syncStatus) AS total
            FROM _
            WHERE syncStatus = $syncStatus
            """);
        query.Parameters = new Parameters().SetString("syncStatus", nameof(SyncStatus.Pending));

        using var results = query.Execute();
        foreach (var row in results)
        {
            return row.GetInt("total");
        }

        return 0;
    }

    public IReadOnlyDictionary<ScanType, int> CountByScanType(string courierId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courierId);

        using var query = Collection.CreateQuery(
            """
            SELECT scanType, COUNT(scanType) AS total
            FROM _
            WHERE courierId = $courierId
            GROUP BY scanType
            """);
        query.Parameters = new Parameters().SetString("courierId", courierId.Trim());

        var counts = new Dictionary<ScanType, int>();
        using var results = query.Execute();
        foreach (var row in results)
        {
            if (Enum.TryParse<ScanType>(row.GetString("scanType"), out var scanType))
            {
                counts[scanType] = row.GetInt("total");
            }
        }

        return counts;
    }

    /// <summary>
    /// The query plan of the tracking-number query. It shows whether the value index is used (for the design note and screenshots).
    /// </summary>
    public string ExplainFindByTrackingNumber()
    {
        using var query = Collection.CreateQuery(
            """
            SELECT META().id AS id, trackingNumber, scanType, courierId, location, scannedAtUtc, notes, syncStatus
            FROM _
            WHERE trackingNumber = $trackingNumber
            ORDER BY scannedAtUtc
            """);
        query.Parameters = new Parameters().SetString("trackingNumber", "EXAMPLE");
        return query.Explain();
    }

    private IReadOnlyList<ShipmentScan> RunScanQuery(string sql, Parameters? parameters = null)
    {
        using var query = Collection.CreateQuery(sql);
        if (parameters is not null)
        {
            query.Parameters = parameters;
        }

        var scans = new List<ShipmentScan>();
        using var results = query.Execute();
        foreach (var row in results)
        {
            scans.Add(ScanDocumentMapper.Read(row));
        }

        return scans;
    }
}
