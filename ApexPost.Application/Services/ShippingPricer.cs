using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

/// <summary>Price = (zone base rate + per-kg rate x chargeable kg) x service level multiplier.</summary>
public sealed class ShippingPricer(ShippingSettings settings) : IShippingPricer
{
    public Money Price(ShipmentType type, ServiceLevel level, Weight weight, ShippingZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var chargeableKg = (type, weight.Grams) switch
        {
            (ShipmentType.Letter, <= 100m) => 0m,
            (ShipmentType.Letter, <= 500m) => 0.5m,
            (ShipmentType.Letter, _) => 1m,
            (ShipmentType.Parcel, _) => decimal.Ceiling(weight.Kilograms),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown shipment type.")
        };

        var multiplier = level switch
        {
            ServiceLevel.Standard => settings.StandardMultiplier,
            ServiceLevel.Express => settings.ExpressMultiplier,
            ServiceLevel.Registered => settings.RegisteredMultiplier,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown service level.")
        };

        var raw = (zone.BaseRate.Amount + zone.PerKgRate.Amount * chargeableKg) * multiplier;
        return new Money(raw, zone.BaseRate.Currency);
    }
}
