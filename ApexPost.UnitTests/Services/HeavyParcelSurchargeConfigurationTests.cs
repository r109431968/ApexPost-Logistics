using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Services;

/// <summary>Added in the REFACTOR step: the threshold and the amount come from configuration, not from code.</summary>
public class HeavyParcelSurchargeConfigurationTests
{
    // Zone: base 10.00 + 2.00 per started kilogram.
    private static readonly ShippingZone Zone = new("Z9", "Test zone", new Money(10m), new Money(2m));

    [Fact]
    public void Price_CustomThreshold_SurchargesParcelsOverTheConfiguredWeight()
    {
        // Arrange
        var pricer = new ShippingPricer(new ShippingSettings { HeavyParcelThresholdKg = 10m });

        // Act
        var atThreshold = pricer.Price(ShipmentType.Parcel, ServiceLevel.Standard, Weight.FromKilograms(10), Zone);
        var overThreshold = pricer.Price(ShipmentType.Parcel, ServiceLevel.Standard, Weight.FromKilograms(12), Zone);

        // Assert
        Assert.Equal(30.00m, atThreshold.Amount);    // 10 + 2*10, no surcharge
        Assert.Equal(49.00m, overThreshold.Amount);  // 10 + 2*12 = 34, plus 15
    }

    [Fact]
    public void Price_CustomSurchargeAmount_IsUsedInsteadOfTheDefault()
    {
        // Arrange
        var pricer = new ShippingPricer(new ShippingSettings { HeavyParcelSurcharge = 5m });

        // Act
        var price = pricer.Price(ShipmentType.Parcel, ServiceLevel.Standard, Weight.FromKilograms(25), Zone);

        // Assert
        Assert.Equal(65.00m, price.Amount);          // 10 + 2*25 = 60, plus 5
    }

    [Fact]
    public void Price_SurchargeSetToZero_SwitchesTheRuleOff()
    {
        var pricer = new ShippingPricer(new ShippingSettings { HeavyParcelSurcharge = 0m });

        var price = pricer.Price(ShipmentType.Parcel, ServiceLevel.Standard, Weight.FromKilograms(25), Zone);

        Assert.Equal(60.00m, price.Amount);
    }
}
