using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Json.Records;

namespace ApexPost.Infrastructure.Persistence.Json.Mapping;

public interface IRecordMapper<TEntity, TRecord>
{
    TRecord ToRecord(TEntity entity);

    TEntity ToEntity(TRecord record);

    /// <summary>The same text as entity.Id.ToString(), so records and entities can be matched.</summary>
    string KeyOf(TRecord record);
}

public sealed class BranchMapper : IRecordMapper<Branch, BranchRecord>
{
    public BranchRecord ToRecord(Branch branch) => new()
    {
        Code = branch.Code.Value,
        Name = branch.Name,
        Region = branch.Region,
        OpensAt = branch.OpensAt,
        ClosesAt = branch.ClosesAt,
        IsActive = branch.IsActive,
        Counters = branch.Counters.Select(c => new CounterRecord { Id = c.Id, Label = c.Label }).ToList()
    };

    public Branch ToEntity(BranchRecord record)
    {
        var branch = new Branch(new BranchCode(record.Code), record.Name, record.Region, record.OpensAt, record.ClosesAt);

        foreach (var counter in record.Counters)
            branch.AddCounter(counter.Id, counter.Label);

        if (!record.IsActive)
            branch.Close();

        return branch;
    }

    public string KeyOf(BranchRecord record) => new BranchCode(record.Code).Value;
}

public sealed class StaffMapper : IRecordMapper<Staff, StaffRecord>
{
    public StaffRecord ToRecord(Staff staff) => new()
    {
        Id = staff.Id,
        Name = staff.Name,
        Email = staff.Email,
        Role = staff.Role,
        HomeBranch = staff.HomeBranch.Value,
        IsActive = staff.IsActive
    };

    public Staff ToEntity(StaffRecord record)
    {
        var staff = new Staff(record.Id, record.Name, record.Email, record.Role, new BranchCode(record.HomeBranch));

        if (!record.IsActive)
            staff.Deactivate();

        return staff;
    }

    public string KeyOf(StaffRecord record) => record.Id.ToString();
}

public sealed class ProductMapper : IRecordMapper<Product, ProductRecord>
{
    public ProductRecord ToRecord(Product product) => new()
    {
        Sku = product.Sku.Value,
        Name = product.Name,
        Category = product.Category,
        UnitPrice = product.UnitPrice.Amount,
        Currency = product.UnitPrice.Currency,
        TaxCategory = product.TaxCategory
    };

    public Product ToEntity(ProductRecord record) => new(
        new Sku(record.Sku),
        record.Name,
        record.Category,
        new Money(record.UnitPrice, record.Currency),
        record.TaxCategory);

    public string KeyOf(ProductRecord record) => new Sku(record.Sku).Value;
}

public sealed class StockMapper : IRecordMapper<StockItem, StockRecord>
{
    public StockRecord ToRecord(StockItem item) => new()
    {
        BranchCode = item.BranchCode.Value,
        Sku = item.Sku.Value,
        Quantity = item.Quantity
    };

    public StockItem ToEntity(StockRecord record) =>
        new(new BranchCode(record.BranchCode), new Sku(record.Sku), record.Quantity);

    public string KeyOf(StockRecord record) =>
        StockItem.KeyFor(new BranchCode(record.BranchCode), new Sku(record.Sku));
}

public sealed class ZoneMapper : IRecordMapper<ShippingZone, ZoneRecord>
{
    public ZoneRecord ToRecord(ShippingZone zone)
    {
        if (zone.BaseRate.Currency != zone.PerKgRate.Currency)
            throw new InvalidOperationException($"Zone {zone.Code} uses two different currencies.");

        return new ZoneRecord
        {
            Code = zone.Code,
            Name = zone.Name,
            BaseRate = zone.BaseRate.Amount,
            PerKgRate = zone.PerKgRate.Amount,
            Currency = zone.BaseRate.Currency
        };
    }

    public ShippingZone ToEntity(ZoneRecord record) => new(
        record.Code,
        record.Name,
        new Money(record.BaseRate, record.Currency),
        new Money(record.PerKgRate, record.Currency));

    public string KeyOf(ZoneRecord record) => record.Code.Trim().ToUpperInvariant();
}

public sealed class TransactionMapper : IRecordMapper<CounterTransaction, TransactionRecord>
{
    private const string RetailKind = "Retail";
    private const string ShippingKind = "Shipping";
    private const string MoneyOrderKind = "MoneyOrder";

    public TransactionRecord ToRecord(CounterTransaction transaction) => new()
    {
        Id = transaction.Id,
        BranchCode = transaction.BranchCode.Value,
        CounterId = transaction.CounterId,
        ClerkId = transaction.ClerkId,
        CreatedAt = transaction.CreatedAt,
        Status = transaction.Status,
        PaidWith = transaction.PaidWith,
        Verification = transaction.Verification is null ? null : ToRecord(transaction.Verification),
        ReversalReason = transaction.ReversalReason,
        TaxTotal = transaction.TaxTotal.Amount,
        Currency = transaction.TaxTotal.Currency,
        Lines = transaction.Lines.Select(ToRecord).ToList()
    };

    public CounterTransaction ToEntity(TransactionRecord record) => CounterTransaction.Restore(
        record.Id,
        new BranchCode(record.BranchCode),
        record.CounterId,
        record.ClerkId,
        record.CreatedAt,
        record.Status,
        record.PaidWith,
        record.Verification is null ? null : ToVerification(record.Verification),
        record.ReversalReason,
        new Money(record.TaxTotal, record.Currency),
        record.Lines.Select(ToLine).ToList());

    public string KeyOf(TransactionRecord record) => record.Id.ToString();

    private static VerificationRecord ToRecord(IdentityVerification verification) => new()
    {
        DocumentType = verification.DocumentType,
        MaskedNumber = verification.MaskedNumber,
        Result = verification.Result,
        VerifiedAt = verification.VerifiedAt
    };

    private static IdentityVerification ToVerification(VerificationRecord record)
    {
        // Only the masked value is stored. Masking an already masked value gives the same text,
        // so Create can be reused; the check below catches a hand-edited or damaged file.
        var restored = IdentityVerification.Create(
            record.DocumentType, record.MaskedNumber, record.Result, record.VerifiedAt);

        if (restored.MaskedNumber != record.MaskedNumber)
            throw new InvalidDataException("A stored identity verification has an invalid masked number.");

        return restored;
    }

    private static LineRecord ToRecord(ILineItem line) => line switch
    {
        RetailLineItem retail => new LineRecord
        {
            Kind = RetailKind,
            Currency = retail.UnitPrice.Currency,
            Sku = retail.Sku.Value,
            Description = retail.Description,
            Quantity = retail.Quantity,
            UnitPrice = retail.UnitPrice.Amount,
            TaxCategory = retail.TaxCategory
        },
        ShippingLineItem shipping => new LineRecord
        {
            Kind = ShippingKind,
            Currency = shipping.UnitPrice.Currency,
            ShipmentType = shipping.ShipmentType,
            WeightGrams = shipping.Weight.Grams,
            DestinationZone = shipping.DestinationZone,
            ServiceLevel = shipping.ServiceLevel,
            UnitPrice = shipping.UnitPrice.Amount
        },
        MoneyOrderLineItem order => new LineRecord
        {
            Kind = MoneyOrderKind,
            Currency = order.Amount.Currency,
            Amount = order.Amount.Amount,
            Fee = order.Fee.Amount,
            Sender = order.Sender,
            Recipient = order.Recipient
        },
        _ => throw new NotSupportedException($"Line item type {line.GetType().Name} cannot be stored.")
    };

    private static ILineItem ToLine(LineRecord line) => line.Kind switch
    {
        RetailKind => new RetailLineItem(
            new Sku(line.Sku ?? throw Missing(line, "Sku")),
            line.Description ?? throw Missing(line, "Description"),
            line.Quantity ?? throw Missing(line, "Quantity"),
            new Money(line.UnitPrice ?? throw Missing(line, "UnitPrice"), line.Currency),
            line.TaxCategory ?? throw Missing(line, "TaxCategory")),

        ShippingKind => new ShippingLineItem(
            line.ShipmentType ?? throw Missing(line, "ShipmentType"),
            Weight.FromGrams(line.WeightGrams ?? throw Missing(line, "WeightGrams")),
            line.DestinationZone ?? throw Missing(line, "DestinationZone"),
            line.ServiceLevel ?? throw Missing(line, "ServiceLevel"),
            new Money(line.UnitPrice ?? throw Missing(line, "UnitPrice"), line.Currency)),

        MoneyOrderKind => new MoneyOrderLineItem(
            new Money(line.Amount ?? throw Missing(line, "Amount"), line.Currency),
            new Money(line.Fee ?? throw Missing(line, "Fee"), line.Currency),
            line.Sender ?? throw Missing(line, "Sender"),
            line.Recipient ?? throw Missing(line, "Recipient")),

        _ => throw new InvalidDataException($"Unknown stored line kind '{line.Kind}'.")
    };

    private static InvalidDataException Missing(LineRecord line, string field) =>
        new($"A stored {line.Kind} line is missing '{field}'.");
}
