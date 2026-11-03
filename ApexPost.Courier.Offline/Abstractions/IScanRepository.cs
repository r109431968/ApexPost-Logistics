using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Abstractions;

/// <summary>
/// Local store of shipment scans on the courier device. The interface uses only domain types.
/// The methods are synchronous because the embedded database call is local and does not wait for a network.
/// </summary>
public interface IScanRepository
{
    /// <summary>Saves a new scan. A new scan is always stored as Pending. Throws if the id already exists.</summary>
    ShipmentScan Create(ShipmentScan scan);

    /// <summary>Key-value lookup by document id. Returns null when there is no such scan.</summary>
    ShipmentScan? GetById(string id);

    /// <summary>Replaces the stored fields of an existing scan. Returns false when the scan does not exist.</summary>
    bool Update(ShipmentScan scan);

    /// <summary>Deletes a scan. Returns false when the scan does not exist.</summary>
    bool Delete(string id);

    IReadOnlyList<ShipmentScan> ListAll();

    /// <summary>All scans of one tracking number, oldest first.</summary>
    IReadOnlyList<ShipmentScan> FindByTrackingNumber(string trackingNumber);

    /// <summary>All scans that still have to be uploaded (syncStatus = Pending), oldest first.</summary>
    IReadOnlyList<ShipmentScan> ListPending();

    int CountPending();

    /// <summary>Number of scans per scan type for one courier. Types without scans are not in the result.</summary>
    IReadOnlyDictionary<ScanType, int> CountByScanType(string courierId);
}
