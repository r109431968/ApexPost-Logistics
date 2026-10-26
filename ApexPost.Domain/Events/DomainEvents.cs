using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Events;

public sealed record TransactionCompletedEvent(
    Guid TransactionId, BranchCode BranchCode, Guid ClerkId, Money Total, DateTimeOffset OccurredAt);

public sealed record StockLowEvent(
    BranchCode BranchCode, Sku Sku, int Remaining, int Threshold, DateTimeOffset OccurredAt);

public sealed record MoneyOrderIssuedEvent(
    Guid TransactionId, BranchCode BranchCode, Money Amount, bool IdentityVerified, DateTimeOffset OccurredAt);

public sealed record TransactionVoidedEvent(
    Guid TransactionId, BranchCode BranchCode, Guid StaffId, string Reason, bool IsRefund, DateTimeOffset OccurredAt);
