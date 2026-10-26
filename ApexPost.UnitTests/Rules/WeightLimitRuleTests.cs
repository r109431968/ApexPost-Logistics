using ApexPost.Application.Settings;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule: parcels over the maximum weight for their service level are rejected.</summary>
public class WeightLimitRuleTests
{
    private static readonly ApexPost.Domain.Rules.WeightLimitRule Rule = new ShippingSettings().CreateWeightLimitRule();

    [Theory]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Standard, 30.0, true)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Standard, 30.001, false)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Express, 20.0, true)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Express, 20.5, false)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Registered, 10.0, true)]
    [InlineData(ShipmentType.Parcel, ServiceLevel.Registered, 10.01, false)]
    [InlineData(ShipmentType.Letter, ServiceLevel.Standard, 2.0, true)]
    [InlineData(ShipmentType.Letter, ServiceLevel.Express, 2.5, false)]
    public void Weight_limits_depend_on_the_type_and_service_level(
        ShipmentType type, ServiceLevel level, double kilograms, bool accepted)
    {
        var weight = Weight.FromKilograms((decimal)kilograms);

        if (accepted)
            Rule.EnsureWithinLimit(type, level, weight);
        else
            Assert.Throws<DomainException>(() => Rule.EnsureWithinLimit(type, level, weight));
    }

    [Fact]
    public void The_error_message_names_the_limit_and_the_actual_weight()
    {
        var error = Assert.Throws<DomainException>(() =>
            Rule.EnsureWithinLimit(ShipmentType.Parcel, ServiceLevel.Registered, Weight.FromKilograms(12)));

        Assert.Contains("10 kg", error.Message);
        Assert.Contains("12 kg", error.Message);
    }

    [Fact]
    public void A_weight_of_zero_or_less_cannot_be_created()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Weight.FromGrams(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Weight.FromKilograms(-1));
    }
}
