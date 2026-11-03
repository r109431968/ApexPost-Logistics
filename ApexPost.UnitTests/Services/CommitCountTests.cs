using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Services;

/// <summary>
/// Proves the "one commit, or none" rule of PayAsync with the spy unit of work: a successful sale commits exactly once
/// (the transaction, its payment and every stock decrement travel together), and a sale that fails commits nothing.
/// </summary>
public class CommitCountTests
{
    [Fact]
    public async Task A_successful_payment_commits_exactly_once()
    {
        var world = await TestWorld.CreateAsync(penStock: 5);
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 2, default);

        var commitsBefore = world.Uow.SaveCount;

        await world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default);

        Assert.Equal(commitsBefore + 1, world.Uow.SaveCount);
        Assert.Equal(3, await world.StockOfAsync(TestWorld.Pen));
    }

    [Fact]
    public async Task A_payment_that_fails_for_missing_stock_never_commits()
    {
        var world = await TestWorld.CreateAsync(penStock: 5);

        var first = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(first.Id, TestWorld.Pen, 4, default);
        var second = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(second.Id, TestWorld.Pen, 3, default);
        await world.Checkout.PayAsync(first.Id, PaymentMethod.Cash, default);

        var commitsBefore = world.Uow.SaveCount;

        await Assert.ThrowsAsync<InsufficientStockException>(
            () => world.Checkout.PayAsync(second.Id, PaymentMethod.Cash, default));

        Assert.Equal(commitsBefore, world.Uow.SaveCount);
    }

    [Fact]
    public async Task A_payment_that_fails_a_domain_rule_never_commits()
    {
        var world = await TestWorld.CreateAsync();
        var emptyBasket = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);

        var commitsBefore = world.Uow.SaveCount;

        await Assert.ThrowsAnyAsync<Exception>(
            () => world.Checkout.PayAsync(emptyBasket.Id, PaymentMethod.Cash, default));

        Assert.Equal(commitsBefore, world.Uow.SaveCount);
    }
}
