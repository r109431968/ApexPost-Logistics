using ApexPost.Application.DTOs;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Infrastructure.Verification;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Services;

public class IdentityVerificationServiceTests
{
    private static readonly SimulatedIdentityVerificationService Service = new(0);

    [Theory]
    [InlineData("P1234567", VerificationResult.Passed)]
    [InlineData("fail9999", VerificationResult.Failed)]
    [InlineData("FAIL-1234", VerificationResult.Failed)]
    [InlineData("PEND1234", VerificationResult.Pending)]
    public async Task The_system_decides_the_result_from_the_document(string number, VerificationResult expected)
    {
        Assert.Equal(expected, await Service.VerifyAsync(DocumentType.Passport, number, default));
    }

    [Fact]
    public async Task A_failed_check_blocks_payment_and_a_clerk_cannot_override_it()
    {
        var world = await TestWorld.CreateAsync();
        var transaction = await world.Checkout.OpenAsync(world.Branch.Code, world.Counter.Id, world.Clerk.Id, default);
        await world.Checkout.AddMoneyOrderAsync(
            transaction.Id, new MoneyOrderRequest { Sender = "Ann", Recipient = "Bob", Amount = 800m }, default);

        var checkedTransaction = await world.Checkout.AttachVerificationAsync(
            transaction.Id, DocumentType.Passport, "FAIL1234", default);

        Assert.Equal(VerificationResult.Failed, checkedTransaction.Verification!.Result);
        await Assert.ThrowsAsync<DomainException>(
            () => world.Checkout.PayAsync(transaction.Id, PaymentMethod.Cash, default));
    }
}