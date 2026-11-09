namespace ApexPost.Domain.Parcels;

/// <summary>
/// The only place that knows which status changes are allowed:
/// Created, PickedUp, InTransit, OutForDelivery, then Delivered or Failed. Delivered and Failed are final.
/// </summary>
public static class ParcelTransitions
{
    public static bool CanMove(ParcelStatus from, ParcelStatus to) => from switch
    {
        ParcelStatus.Created => to == ParcelStatus.PickedUp,
        ParcelStatus.PickedUp => to == ParcelStatus.InTransit,
        ParcelStatus.InTransit => to == ParcelStatus.OutForDelivery,
        ParcelStatus.OutForDelivery => to is ParcelStatus.Delivered or ParcelStatus.Failed,
        _ => false
    };

    public static bool IsFinal(ParcelStatus status) => status is ParcelStatus.Delivered or ParcelStatus.Failed;

    /// <summary>
    /// A courier scan can move a parcel forward, and can skip steps (a courier may deliver without a separate
    /// "out for delivery" scan). It never moves a parcel backwards and never changes a final parcel.
    /// Returns null when the scan leaves the status as it is.
    /// </summary>
    public static ParcelStatus? StatusAfterScan(ParcelStatus current, ParcelScanType scan)
    {
        if (IsFinal(current))
            return null;

        var target = scan switch
        {
            ParcelScanType.Pickup => ParcelStatus.PickedUp,
            ParcelScanType.InTransit => ParcelStatus.InTransit,
            ParcelScanType.Delivered => ParcelStatus.Delivered,
            _ => ParcelStatus.Failed
        };

        return target > current ? target : null;
    }
}
