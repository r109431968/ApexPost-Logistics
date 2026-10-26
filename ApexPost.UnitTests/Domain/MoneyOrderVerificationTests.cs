using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Domain;

/// <summary>Rule: a money order above the threshold cannot be completed without a passed identity verification.</summary>
public class MoneyOrderVerificationTests
{
    private static readonly Money Threshold = new(300m);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    private static (Branch Branch, Staff Clerk, CounterTransaction Transaction) NewTransaction(decimal orderAmount)
    {
        var code = new BranchCode("BR001");
        var branch = new Branch(code, "Central", "Central", new TimeOnly(8, 0), new TimeOnly(18, 0));
        var counter = branch.AddCounter("Counter 1");
        var clerk = new Staff(Guid.NewGuid(), "Casey Clerk", "casey@apexpost.test", StaffRole.CounterClerk, code);

        var transaction = CounterTransaction.Open(branch, counter, clerk, Now);
        transaction.AddLine(new MoneyOrderLineItem(new Money(orderAmount), new Money(6m), "Sender", "Recipient"));
        return (branch, clerk, transaction);
    }

    private static Money NoTax(ILineItem line) => Money.Zero;

    private static IdentityVerification Verification(VerificationResult result) =>
        IdentityVerification.Create(DocumentType.Passport, "P1234567", result, Now);

    [Fact]
    public void Above_threshold_without_verification_cannot_be_paid()
    {
        var (_, _, transaction) = NewTransaction(500m);

        var error = Assert.Throws<DomainException>(() => transaction.Pay(PaymentMethod.Cash, Threshold, NoTax));

        Assert.Contains("identity verification", error.Message);
        Assert.Equal(TransactionStatus.Open, transaction.Status);
    }

    [Theory]
    [InlineData(VerificationResult.Failed)]
    [InlineData(VerificationResult.Pending)]
    public void Above_threshold_with_a_failed_or_pending_verification_cannot_be_paid(VerificationResult result)
    {
        var (_, _, transaction) = NewTransaction(500m);
        transaction.AttachVerification(Verification(result));

        Assert.Throws<DomainException>(() => transaction.Pay(PaymentMethod.Cash, Threshold, NoTax));
        Assert.Equal(TransactionStatus.Open, transaction.Status);
    }

    [Fact]
    public void Above_threshold_with_a_passed_verification_can_be_paid()
    {
        var (_, _, transaction) = NewTransaction(500m);
        transaction.AttachVerification(Verification(VerificationResult.Passed));

        transaction.Pay(PaymentMethod.Card, Threshold, NoTax);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(300)]
    public void At_or_below_the_threshold_no_verification_is_needed(decimal amount)
    {
        var (_, _, transaction) = NewTransaction(amount);

        transaction.Pay(PaymentMethod.Cash, Threshold, NoTax);

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    [Fact]
    public void Just_above_the_threshold_needs_verification()
    {
        var (_, _, transaction) = NewTransaction(300.01m);

        Assert.Throws<DomainException>(() => transaction.Pay(PaymentMethod.Cash, Threshold, NoTax));
    }

    [Fact]
    public void The_document_number_is_stored_masked_with_only_the_last_four_characters()
    {
        var verification = IdentityVerification.Create(
            DocumentType.NationalId, "AB-123456789", VerificationResult.Passed, Now);

        Assert.Equal("****6789", verification.MaskedNumber);
        Assert.DoesNotContain("AB-12345", verification.MaskedNumber);
    }

    [Fact]
    public void A_document_number_shorter_than_four_characters_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            IdentityVerification.Create(DocumentType.Passport, "123", VerificationResult.Passed, Now));
    }

    [Fact]
    public async Task The_service_enforces_the_same_rule_end_to_end()
    {
        var world = await TestWorld.CreateAsync();
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);

        await world.Checkout.AddMoneyOrderAsync(
            transaction.Id,
            new ApexPost.Application.DTOs.MoneyOrderRequest { Sender = "Ann", Recipient = "Bob", Amount = 800m },
            default);

        await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default));

        await world.Checkout.AttachVerificationAsync(
            transaction.Id, DocumentType.Passport, "P7654321", VerificationResult.Passed, default);

        var paid = await world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default);

        Assert.Equal(TransactionStatus.Paid, paid.Status);
        Assert.Equal("****4321", paid.Verification!.MaskedNumber);
    }
}
