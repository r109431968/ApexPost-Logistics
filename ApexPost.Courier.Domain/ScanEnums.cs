namespace ApexPost.Courier.Offline.Domain;

/// <summary>What the courier did with the shipment at this scan.</summary>
public enum ScanType
{
    Pickup,
    InTransit,
    Delivered,
    Failed
}

/// <summary>Where the scan is on its way to the central system. New scans always start as Pending.</summary>
public enum SyncStatus
{
    Pending,
    Synced,
    Failed
}
