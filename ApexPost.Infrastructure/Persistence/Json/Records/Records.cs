using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

// Storage records are plain data with no rules. The Domain never learns how it is stored.

public sealed record CounterRecord
{
    public required Guid Id { get; init; }
    public required string Label { get; init; }
}

public sealed record BranchRecord
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Region { get; init; }
    public required TimeOnly OpensAt { get; init; }
    public required TimeOnly ClosesAt { get; init; }
    public required bool IsActive { get; init; }
    public List<CounterRecord> Counters { get; init; } = [];
}

public sealed record StaffRecord
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required StaffRole Role { get; init; }
    public required string HomeBranch { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record ProductRecord
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required ProductCategory Category { get; init; }
    public required decimal UnitPrice { get; init; }
    public required string Currency { get; init; }
    public required TaxCategory TaxCategory { get; init; }
}

public sealed record StockRecord
{
    public required string BranchCode { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}

public sealed record ZoneRecord
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal BaseRate { get; init; }
    public required decimal PerKgRate { get; init; }
    public required string Currency { get; init; }
}

public sealed record VerificationRecord
{
    public required DocumentType DocumentType { get; init; }
    public required string MaskedNumber { get; init; }
    public required VerificationResult Result { get; init; }
    public required DateTimeOffset VerifiedAt { get; init; }
}

public sealed record LineRecord
{
    public required string Kind { get; init; }
    public required string Currency { get; init; }

    public decimal? UnitPrice { get; init; }
    public TaxCategory? TaxCategory { get; init; }

    public string? Sku { get; init; }
    public string? Description { get; init; }
    public int? Quantity { get; init; }

    public ShipmentType? ShipmentType { get; init; }
    public decimal? WeightGrams { get; init; }
    public string? DestinationZone { get; init; }
    public ServiceLevel? ServiceLevel { get; init; }

    public decimal? Amount { get; init; }
    public decimal? Fee { get; init; }
    public string? Sender { get; init; }
    public string? Recipient { get; init; }
}

public sealed record TransactionRecord
{
    public required Guid Id { get; init; }
    public required string BranchCode { get; init; }
    public required Guid CounterId { get; init; }
    public required Guid ClerkId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required TransactionStatus Status { get; init; }
    public PaymentMethod? PaidWith { get; init; }
    public VerificationRecord? Verification { get; init; }
    public string? ReversalReason { get; init; }
    public required decimal TaxTotal { get; init; }
    public required string Currency { get; init; }
    public List<LineRecord> Lines { get; init; } = [];
}
