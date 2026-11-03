using System.Globalization;
using ApexPost.Courier.Offline.Domain;
using Couchbase.Lite;
using Couchbase.Lite.Query;

namespace ApexPost.Courier.Offline.Storage;

/// <summary>
/// Converts between the domain record and the JSON document. This is the only place that knows the document field names.
/// Times are stored as fixed-width UTC text (yyyy-MM-ddTHH:mm:ss.fffZ), so ordering by the text is ordering by time.
/// </summary>
internal static class ScanDocumentMapper
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static void Write(MutableDocument document, ShipmentScan scan)
    {
        document.SetString("trackingNumber", scan.TrackingNumber);
        document.SetString("scanType", scan.ScanType.ToString());
        document.SetString("courierId", scan.CourierId);
        document.SetString("location", scan.Location);
        document.SetString("scannedAtUtc", scan.ScannedAtUtc.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture));
        if (scan.Notes is null)
        {
            document.Remove("notes");
        }
        else
        {
            document.SetString("notes", scan.Notes);
        }

        document.SetString("syncStatus", scan.SyncStatus.ToString());
    }

    public static ShipmentScan Read(Document document) => Build(
        document.Id,
        document.GetString("trackingNumber"),
        document.GetString("scanType"),
        document.GetString("courierId"),
        document.GetString("location"),
        document.GetString("scannedAtUtc"),
        document.GetString("notes"),
        document.GetString("syncStatus"));

    public static ShipmentScan Read(Result row) => Build(
        row.GetString("id"),
        row.GetString("trackingNumber"),
        row.GetString("scanType"),
        row.GetString("courierId"),
        row.GetString("location"),
        row.GetString("scannedAtUtc"),
        row.GetString("notes"),
        row.GetString("syncStatus"));

    private static ShipmentScan Build(
        string? id, string? trackingNumber, string? scanType, string? courierId,
        string? location, string? scannedAtUtc, string? notes, string? syncStatus)
    {
        if (id is null || trackingNumber is null || courierId is null || location is null || scannedAtUtc is null)
        {
            throw new InvalidDataException($"The scan document '{id}' is missing a required field.");
        }

        if (!Enum.TryParse<ScanType>(scanType, out var parsedScanType))
        {
            throw new InvalidDataException($"The scan document '{id}' has an unknown scanType '{scanType}'.");
        }

        if (!Enum.TryParse<SyncStatus>(syncStatus, out var parsedSyncStatus))
        {
            throw new InvalidDataException($"The scan document '{id}' has an unknown syncStatus '{syncStatus}'.");
        }

        return new ShipmentScan
        {
            Id = id,
            TrackingNumber = trackingNumber,
            ScanType = parsedScanType,
            CourierId = courierId,
            Location = location,
            ScannedAtUtc = DateTimeOffset.Parse(
                scannedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            Notes = notes,
            SyncStatus = parsedSyncStatus
        };
    }
}
