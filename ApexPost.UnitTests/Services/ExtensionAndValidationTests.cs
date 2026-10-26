using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Validation;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Services;

public class ExtensionAndValidationTests
{
    [Theory]
    [InlineData(10.02, 10.00)]
    [InlineData(10.03, 10.05)]
    [InlineData(10.07, 10.05)]
    [InlineData(10.08, 10.10)]
    public void Cash_totals_round_to_the_nearest_five_cents(double amount, double expected)
    {
        Assert.Equal((decimal)expected, new Money((decimal)amount).RoundedForCash().Amount);
    }

    [Fact]
    public void Percent_and_Total_extension_members_work_on_money()
    {
        Assert.Equal(10m, new Money(50m).Percent(0.20m).Amount);
        Assert.Equal(7m, new[] { new Money(2m), new Money(5m) }.Total().Amount);
        Assert.True(new Money(1m).IsPositive);
        Assert.False(Money.Zero.IsPositive);
    }

    [Fact]
    public async Task Basket_summary_properties_split_retail_from_services()
    {
        var world = await Support.TestWorld.CreateAsync();
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(transaction.Id, Support.TestWorld.Pen, 2, default);        // 5.00
        await world.Checkout.AddShippingLineAsync(
            transaction.Id, ShipmentType.Parcel, Weight.FromKilograms(1), "Z1", ServiceLevel.Standard, default);   // (5+1)*1.0 = 6.00

        var loaded = (await world.Transactions.GetByIdAsync(transaction.Id, default))!;

        Assert.Equal(5.00m, loaded.RetailSubtotal.Amount);
        Assert.Equal(6.00m, loaded.ServicesSubtotal.Amount);
        Assert.Equal(3, loaded.ItemCount);
        Assert.Equal(1, loaded.ShippingCount);
        Assert.False(loaded.HasMoneyOrder);
    }

    private readonly AttributeModelValidator _validator = new();

    [Fact]
    public void A_valid_money_order_request_has_no_failures()
    {
        var request = new MoneyOrderRequest { Sender = "Ann", Recipient = "Bob", Amount = 25m };

        Assert.Empty(_validator.Validate(request));
    }

    [Fact]
    public void Blank_names_and_a_non_positive_amount_are_reported_per_member()
    {
        var request = new MoneyOrderRequest { Sender = " ", Recipient = "", Amount = 0m };

        var failures = _validator.Validate(request);

        Assert.Contains(failures, f => f.Member == nameof(MoneyOrderRequest.Sender));
        Assert.Contains(failures, f => f.Member == nameof(MoneyOrderRequest.Recipient));
        Assert.Contains(failures, f => f.Member == nameof(MoneyOrderRequest.Amount));
    }

    [Fact]
    public void Staff_request_checks_email_and_branch_code_patterns()
    {
        var request = new StaffRequest { Name = "Ann", Email = "not-an-email", Role = StaffRole.Auditor, HomeBranch = "X1" };

        var failures = _validator.Validate(request);

        Assert.Contains(failures, f => f.Member == nameof(StaffRequest.Email));
        Assert.Contains(failures, f => f.Member == nameof(StaffRequest.HomeBranch));
    }

    [Fact]
    public void Reflection_metadata_is_cached_per_type()
    {
        var request = new MoneyOrderRequest { Sender = "Ann", Recipient = "Bob", Amount = 5m };

        _validator.Validate(request);
        var afterFirst = AttributeModelValidator.CachedTypeCount;
        _validator.Validate(request);
        _validator.Validate(request);

        Assert.True(afterFirst >= 1);
        Assert.Equal(afterFirst, AttributeModelValidator.CachedTypeCount);
    }
}
