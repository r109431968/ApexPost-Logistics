using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Rules;

/// <summary>Parcels and letters over the maximum weight for their service level are rejected.</summary>
public sealed class WeightLimitRule(
    Weight letterMax,
    Weight standardParcelMax,
    Weight expressParcelMax,
    Weight registeredParcelMax)
{
    public Weight MaximumFor(ShipmentType type, ServiceLevel level) => (type, level) switch
    {
        (ShipmentType.Letter, _) => letterMax,
        (ShipmentType.Parcel, ServiceLevel.Standard) => standardParcelMax,
        (ShipmentType.Parcel, ServiceLevel.Express) => expressParcelMax,
        (ShipmentType.Parcel, ServiceLevel.Registered) => registeredParcelMax,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown shipment type or service level.")
    };

    public void EnsureWithinLimit(ShipmentType type, ServiceLevel level, Weight weight)
    {
        var max = MaximumFor(type, level);
        if (weight > max)
            throw new DomainException(
                $"A {level} {type} cannot weigh more than {max}; this one weighs {weight}.");
    }
}
