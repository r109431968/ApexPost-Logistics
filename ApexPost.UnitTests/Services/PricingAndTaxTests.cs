using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Services;

public class PricingAndTaxTests
{
    // Zone: base 10.00 + 2.00 per started kilogram.
    private static readonly ShippingZone Zone = new("Z9", "Test zone", new Money(10m), new Money(2m));
    private static readonly ShippingPricer Pricer = new(new ShippingSettings());

    [Theory]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Standard, 3000, 16.00)]     // (10 + 2*3) * 1.0
    [InlineData(ShipmentType.Parcel, ServiceLevel.Express, 2100, 25.60)]      // 2.1 kg rounds up to 3 kg: 16 * 1.6
    [InlineData(ShipmentType.Parcel, ServiceLevel.Registered, 1000, 15.60)]   // exactly 1 kg: 12 * 1.3
    [InlineData(ShipmentType.Parcel, ServiceLevel.Standard, 1001, 14.00)]     // 1.001 kg rounds up to 2 kg
    [InlineData(ShipmentType.Letter, ServiceLevel.Standard, 80, 10.00)]       // light letter: base only
    [InlineData(ShipmentType.Letter, ServiceLevel.Registered, 300, 14.30)]    // (10 + 2*0.5) * 1.3
    [InlineData(ShipmentType.Letter, ServiceLevel.Standard, 600, 12.00)]      // heavy letter: 1 kg band
    public void Price_follows_the_zone_rate_the_weight_band_and_the_service_level(
        ShipmentType type, ServiceLevel level, int grams, double expected)
    {
        var price = Pricer.Price(type, level, Weight.FromGrams(grams), Zone);

        Assert.Equal((decimal)expected, price.Amount);
        Assert.Equal("USD", price.Currency);
    }

    [Fact]
    public void Multipliers_come_from_configuration_not_from_code()
    {
        var pricer = new ShippingPricer(new ShippingSettings { ExpressMultiplier = 2m });

        var price = pricer.Price(ShipmentType.Parcel, ServiceLevel.Express, Weight.FromKilograms(1), Zone);

        Assert.Equal(24.00m, price.Amount);   // (10 + 2) * 2
    }

    [Fact]
    public void An_unknown_service_level_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Pricer.Price(ShipmentType.Parcel, (ServiceLevel)99, Weight.FromKilograms(1), Zone));
    }

    private static readonly TaxCalculator Tax = new(new TaxSettings());

    [Fact]
    public void Standard_rated_goods_are_taxed_at_the_standard_rate()
    {
        var line = new RetailLineItem(new Sku("STA-0001"), "Pen", 3, new Money(2.50m), TaxCategory.Standard);

        Assert.Equal(1.50m, Tax.TaxFor(line).Amount);   // 7.50 * 20%
    }

    [Fact]
    public void Reduced_rated_goods_are_taxed_at_the_reduced_rate()
    {
        var line = new RetailLineItem(new Sku("PKG-0001"), "Box", 10, new Money(1.20m), TaxCategory.Reduced);

        Assert.Equal(0.60m, Tax.TaxFor(line).Amount);   // 12.00 * 5%
    }

    [Fact]
    public void Money_orders_are_tax_exempt()
    {
        var line = new MoneyOrderLineItem(new Money(500m), new Money(10m), "Ann", "Bob");

        Assert.Equal(0m, Tax.TaxFor(line).Amount);
    }

    [Fact]
    public void Shipping_is_taxed_at_the_standard_rate()
    {
        var line = new ShippingLineItem(ShipmentType.Parcel, Weight.FromKilograms(3), "Z9", ServiceLevel.Standard, new Money(16m));

        Assert.Equal(3.20m, Tax.TaxFor(line).Amount);
    }

    [Fact]
    public void AsRule_returns_a_delegate_that_gives_the_same_answer()
    {
        var line = new RetailLineItem(new Sku("STA-0001"), "Pen", 1, new Money(10m), TaxCategory.Standard);
        Func<ILineItem, Money> rule = Tax.AsRule();

        Assert.Equal(Tax.TaxFor(line), rule(line));
    }

    [Fact]
    public void Money_rounds_half_away_from_zero_to_two_decimals()
    {
        Assert.Equal(0.13m, new Money(0.125m).Amount);
        Assert.Equal(0.12m, new Money(0.124m).Amount);
    }

    [Fact]
    public void Money_cannot_be_negative_or_mix_currencies()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-0.01m));
        Assert.Throws<InvalidOperationException>(() => new Money(1m, "USD") + new Money(1m, "EUR"));
    }
}
