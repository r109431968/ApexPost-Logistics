using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Services;

/// <summary>
/// Written test-first (see TDD-LOG.md).
/// Rule: a parcel over 20 kg gets a flat heavy-parcel surcharge of 15.00. The surcharge is added AFTER the service level
/// multiplier (it is not multiplied). Exactly 20 kg is not surcharged. Letters are never surcharged.
/// </summary>
public class HeavyParcelSurchargeTests
{
    // Zone: base 10.00 + 2.00 per started kilogram.
    private static readonly ShippingZone Zone = new("Z9", "Test zone", new Money(10m), new Money(2m));
    private static readonly ShippingPricer Pricer = new(new ShippingSettings());

    [Theory]
    [InlineData(20000, 50.00)]   // exactly 20 kg: 10 + 2*20, no surcharge (boundary)
    [InlineData(20001, 67.00)]   // 20.001 kg rounds up to 21 kg: 10 + 2*21 = 52, plus 15 (just over the boundary)
    [InlineData(25000, 75.00)]   // 10 + 2*25 = 60, plus 15
    [InlineData(30000, 85.00)]   // 10 + 2*30 = 70, plus 15 (the heaviest standard parcel)
    public void Price_ParcelWeight_AddsFlatSurchargeOnlyAboveTwentyKilograms(int grams, double expected)
    {
        // Act
        var price = Pricer.Price(ShipmentType.Parcel, ServiceLevel.Standard, Weight.FromGrams(grams), Zone);

        // Assert
        Assert.Equal((decimal)expected, price.Amount);
    }

    [Fact]
    public void Price_HeavyExpressParcel_SurchargeIsNotMultipliedByTheServiceLevel()
    {
        // Act: (10 + 2*25) * 1.6 = 96, then + 15 = 111. If the surcharge were multiplied it would be 120.
        var price = Pricer.Price(ShipmentType.Parcel, ServiceLevel.Express, Weight.FromKilograms(25), Zone);

        // Assert
        Assert.Equal(111.00m, price.Amount);
    }

    [Fact]
    public void Price_HeavyLetter_IsNeverSurcharged()
    {
        // Act: the pricer does not check weight limits (the counter does), so it must not surcharge a letter by itself.
        var price = Pricer.Price(ShipmentType.Letter, ServiceLevel.Standard, Weight.FromKilograms(25), Zone);

        // Assert: heavy letter band is 1 kg: 10 + 2*1.
        Assert.Equal(12.00m, price.Amount);
    }
}
