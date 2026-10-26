using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Domain;

/// <summary>Staff, branch, void and refund rules.</summary>
public class TransactionRulesTests
{
    [Fact]
    public async Task An_inactive_clerk_cannot_open_a_transaction()
    {
        var world = await TestWorld.CreateAsync();
        world.Clerk.Deactivate();
        await world.StaffRepo.UpdateAsync(world.Clerk, default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default));
    }

    [Fact]
    public async Task A_clerk_cannot_transact_at_another_branch()
    {
        var world = await TestWorld.CreateAsync();
        var other = new BranchCode("BR002");
        var otherBranch = new Branch(other, "Harbour", "South", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var otherCounter = otherBranch.AddCounter("Counter 1");
        await world.Branches.AddAsync(otherBranch, default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.OpenAsync(other, otherCounter.Id, world.Clerk.Id, default));
    }

    [Fact]
    public async Task A_closed_branch_cannot_accept_transactions()
    {
        var world = await TestWorld.CreateAsync(branchActive: false);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default));

        Assert.Contains("closed branch", error.Message);
    }

    [Fact]
    public async Task Auditors_and_administrators_cannot_process_transactions()
    {
        var world = await TestWorld.CreateAsync();
        var auditor = new Staff(Guid.NewGuid(), "Ada Auditor", "ada@apexpost.test", StaffRole.Auditor, world.Branch.Code);
        await world.StaffRepo.AddAsync(auditor, default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, auditor.Id, default));
    }

    [Fact]
    public async Task A_clerk_cannot_void_but_a_manager_can_with_a_reason_and_stock_returns()
    {
        var world = await TestWorld.CreateAsync(penStock: 10);
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 4, default);
        await world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default);
        Assert.Equal(6, await world.StockOfAsync(TestWorld.Pen));

        await Assert.ThrowsAsync<DomainException>(
            () => world.Reversal.VoidAsync(transaction.Id, world.Clerk.Id, "customer changed mind", default));

        var voided = await world.Reversal.VoidAsync(transaction.Id, world.Manager.Id, "customer changed mind", default);

        Assert.Equal(TransactionStatus.Voided, voided.Status);
        Assert.Equal(10, await world.StockOfAsync(TestWorld.Pen));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_void_without_a_reason_is_rejected(string reason)
    {
        var world = await TestWorld.CreateAsync();
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 1, default);
        await world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Reversal.VoidAsync(transaction.Id, world.Manager.Id, reason, default));
    }

    [Fact]
    public async Task A_refund_needs_a_paid_transaction()
    {
        var world = await TestWorld.CreateAsync();
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 1, default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Reversal.RefundAsync(transaction.Id, world.Manager.Id, "not paid yet", default));
    }

    [Fact]
    public async Task A_transaction_with_no_lines_cannot_be_paid()
    {
        var world = await TestWorld.CreateAsync();
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default));
    }

    [Fact]
    public async Task Paying_down_to_the_threshold_raises_the_StockLow_event()
    {
        var world = await TestWorld.CreateAsync(penStock: 8);
        var raised = new List<ApexPost.Domain.Events.StockLowEvent>();
        world.Events.StockLow += (_, e) => raised.Add(e);

        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddRetailLineAsync(transaction.Id, TestWorld.Pen, 4, default);   // 8 - 4 = 4, below 5
        await world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default);

        var alert = Assert.Single(raised);
        Assert.Equal(TestWorld.Pen, alert.Sku);
        Assert.Equal(4, alert.Remaining);
    }
}
