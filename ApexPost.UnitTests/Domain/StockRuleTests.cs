using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Domain;

/// <summary>Rule: branch stock can never go negative. A sale fails if stock is insufficient.</summary>
public class StockRuleTests
{
    private static StockItem Item(int quantity) => new(new BranchCode("BR001"), new Sku("PKG-0001"), quantity);

    [Fact]
    public void Removing_more_than_available_is_rejected_and_stock_is_unchanged()
    {
        var item = Item(3);

        var error = Assert.Throws<InsufficientStockException>(() => item.Remove(4));

        Assert.Equal(3, item.Quantity);
        Assert.Contains("available 3, requested 4", error.Message);
    }

    [Fact]
    public void Removing_exactly_what_is_available_leaves_zero_not_negative()
    {
        var item = Item(3);

        item.Remove(3);

        Assert.Equal(0, item.Quantity);
    }

    [Fact]
    public void Stock_cannot_be_created_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Item(-1));
    }

    [Fact]
    public async Task Adding_more_to_the_basket_than_is_in_stock_fails()
    {
        var world = await TestWorld.CreateAsync(penStock: 5);
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);

        await world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 3, default);

        // 3 already in the basket + 3 more = 6 > 5
        await Assert.ThrowsAsync<InsufficientStockException>(
            () => world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 3, default));
    }

    [Fact]
    public async Task Paying_reduces_stock_and_a_second_sale_beyond_what_is_left_fails_without_changing_stock()
    {
        var world = await TestWorld.CreateAsync(penStock: 5);

        var first = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(first.Id, TestWorld.Pen, 4, default);

        // Second basket is built while 5 are still on the shelf, so the basket itself is accepted.
        var second = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(second.Id, TestWorld.Pen, 3, default);

        await world.Checkout.PayAsync(first.Id, PaymentMethod.Cash, default);
        Assert.Equal(1, await world.StockOfAsync(TestWorld.Pen));

        await Assert.ThrowsAsync<InsufficientStockException>(
            () => world.Checkout.PayAsync(second.Id, PaymentMethod.Cash, default));

        Assert.Equal(1, await world.StockOfAsync(TestWorld.Pen));
    }
}
