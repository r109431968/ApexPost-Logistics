using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence.Sql.Entities;

// Persistence entities: plain data shapes for SQL Server. They hold no business rules.
// The rich domain classes stay untouched; mappers (added with the repositories) translate between the two.

public sealed class RoleEntity
{
    public StaffRole Id { get; set; }
    public string Name { get; set; } = null!;
}

public sealed class ProductCategoryEntity
{
    public ProductCategory Id { get; set; }
    public string Name { get; set; } = null!;
}

public sealed class BranchEntity
{
    public BranchCode Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Region { get; set; } = null!;
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsActive { get; set; }
    public List<CounterEntity> Counters { get; set; } = [];
}

public sealed class CounterEntity
{
    public Guid Id { get; set; }
    public BranchCode BranchCode { get; set; } = null!;
    public string Label { get; set; } = null!;
    public BranchEntity? Branch { get; set; }
}

public sealed class StaffEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public StaffRole RoleId { get; set; }
    public BranchCode HomeBranchCode { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? PasswordHash { get; set; }
}

public sealed class ProductEntity
{
    public Sku Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public ProductCategory CategoryId { get; set; }
    public Money UnitPrice { get; set; } = null!;
    public TaxCategory TaxCategory { get; set; }
}

public sealed class BranchStockEntity
{
    public BranchCode BranchCode { get; set; } = null!;
    public Sku Sku { get; set; } = null!;
    public int Quantity { get; set; }
}

public sealed class StockAdjustmentEntity
{
    public long Id { get; set; }
    public BranchCode BranchCode { get; set; } = null!;
    public Sku Sku { get; set; } = null!;
    public int Delta { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = null!;
    public string ChangedBy { get; set; } = null!;
    public DateTime ChangedAtUtc { get; set; }
}

public sealed class ShippingZoneEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public ShippingRateEntity? Rate { get; set; }
}

public sealed class ShippingRateEntity
{
    public string ZoneCode { get; set; } = null!;
    public Money BaseRate { get; set; } = null!;
    public Money PerKgRate { get; set; } = null!;
    public ShippingZoneEntity? Zone { get; set; }
}

public sealed class CounterTransactionEntity
{
    public Guid Id { get; set; }
    public BranchCode BranchCode { get; set; } = null!;
    public Guid CounterId { get; set; }
    public Guid ClerkId { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public TransactionStatus Status { get; set; }
    public Money TaxTotal { get; set; } = null!;
    public string? ReversalReason { get; set; }

    public List<TransactionLineEntity> Lines { get; set; } = [];
    public PaymentEntity? Payment { get; set; }
    public IdentityVerificationEntity? Verification { get; set; }
}

public sealed class TransactionLineEntity
{
    public Guid TransactionId { get; set; }
    public int LineNumber { get; set; }
    public string LineType { get; set; } = null!;
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; } = null!;
    public TaxCategory TaxCategory { get; set; }
    public Sku? Sku { get; set; }

    public CounterTransactionEntity? Transaction { get; set; }
    public ShipmentEntity? Shipment { get; set; }
    public MoneyOrderEntity? MoneyOrder { get; set; }
}

public sealed class ShipmentEntity
{
    public Guid TransactionId { get; set; }
    public int LineNumber { get; set; }
    public ShipmentType ShipmentType { get; set; }
    public Weight Weight { get; set; } = null!;
    public string DestinationZone { get; set; } = null!;
    public ServiceLevel ServiceLevel { get; set; }
    public TransactionLineEntity? Line { get; set; }
}

public sealed class MoneyOrderEntity
{
    public Guid TransactionId { get; set; }
    public int LineNumber { get; set; }
    public Money Amount { get; set; } = null!;
    public Money Fee { get; set; } = null!;
    public string Sender { get; set; } = null!;
    public string Recipient { get; set; } = null!;
    public TransactionLineEntity? Line { get; set; }
}

public sealed class PaymentEntity
{
    public Guid TransactionId { get; set; }
    public PaymentMethod Method { get; set; }
    public Money Amount { get; set; } = null!;
    public CounterTransactionEntity? Transaction { get; set; }
}

public sealed class IdentityVerificationEntity
{
    public Guid TransactionId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string MaskedNumber { get; set; } = null!;
    public VerificationResult Result { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public CounterTransactionEntity? Transaction { get; set; }
}

public sealed class AuditEntryEntity
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public Guid? StaffId { get; set; }
    public string? Role { get; set; }
    public string? BranchCode { get; set; }
    public string Controller { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string? Operation { get; set; }
    public string RouteValuesJson { get; set; } = "{}";
    public string DetailsJson { get; set; } = "{}";
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string CorrelationId { get; set; } = null!;
}

public sealed class SyncInboxEntity
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = null!;
    public string LocalTransactionId { get; set; } = null!;
    public Guid? TransactionId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string Outcome { get; set; } = null!;
}