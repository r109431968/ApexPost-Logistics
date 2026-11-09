namespace ApexPost.Domain.Parcels;

/// <summary>Where a parcel is on its way. The numbers are stored in the database; never reorder them.</summary>
public enum ParcelStatus
{
    Created = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5
}

/// <summary>What a courier recorded when scanning a parcel. Same four values as the offline store of Part A.</summary>
public enum ParcelScanType
{
    Pickup = 0,
    InTransit = 1,
    Delivered = 2,
    Failed = 3
}
