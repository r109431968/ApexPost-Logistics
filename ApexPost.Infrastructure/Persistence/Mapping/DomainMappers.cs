using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Sql.Entities;

namespace ApexPost.Infrastructure.Persistence.Sql.Mapping;

// Mappers translate between the rich domain classes and the plain SQL entities.
// ToDomain rebuilds a domain object, ToEntity creates a new row, Apply copies changes onto a tracked row.
// Nothing here makes a business decision, and no mapper calls the database.

internal static class DateMapping
{
    public static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

internal static class BranchMapper
{
    public static Branch ToDomain(BranchEntity entity)
    {
        var branch = new Branch(entity.Code, entity.Name, entity.Region, entity.OpensAt, entity.ClosesAt);

        foreach (var counter in entity.Counters.OrderBy(c => c.Label, StringComparer.Ordinal))
            branch.AddCounter(counter.Id, counter.Label);

        if (entity.Address is { } stored)
        {
            branch.MoveTo(new Address
            {
                Line1 = stored.Line1, City = stored.City, Postcode = stored.Postcode, Country = stored.Country
            });
        }

        if (!entity.IsActive)
            branch.Close();

        return branch;
    }

    public static BranchEntity ToEntity(Branch branch)
    {
        var entity = new BranchEntity { Code = branch.Code };
        Apply(branch, entity);
        return entity;
    }

    public static void Apply(Branch branch, BranchEntity entity)
    {
        entity.Name = branch.Name;
        entity.Region = branch.Region;
        entity.OpensAt = branch.OpensAt;
        entity.ClosesAt = branch.ClosesAt;
        entity.IsActive = branch.IsActive;
        entity.Address = branch.Address is { } address
            ? new AddressValue { Line1 = address.Line1, City = address.City, Postcode = address.Postcode, Country = address.Country }
            : null;

        foreach (var counter in branch.Counters)
        {
            if (entity.Counters.All(existing => existing.Id != counter.Id))
                entity.Counters.Add(new CounterEntity { Id = counter.Id, BranchCode = branch.Code, Label = counter.Label });
        }
    }
}

internal static class StaffMapper
{
    public static Staff ToDomain(StaffEntity entity)
    {
        var staff = new Staff(entity.Id, entity.Name, entity.Email, entity.RoleId, entity.HomeBranchCode);

        if (!entity.IsActive)
            staff.Deactivate();

        return staff;
    }

    public static StaffEntity ToEntity(Staff staff)
    {
        var entity = new StaffEntity { Id = staff.Id };
        Apply(staff, entity);
        return entity;
    }

    /// <summary>The password hash is not copied here. Only the credential store touches it.</summary>
    public static void Apply(Staff staff, StaffEntity entity)
    {
        entity.Name = staff.Name;
        entity.Email = staff.Email;
        entity.RoleId = staff.Role;
        entity.HomeBranchCode = staff.HomeBranch;
        entity.IsActive = staff.IsActive;
    }
}

internal static class ProductMapper
{
    public static Product ToDomain(ProductEntity entity) =>
        new(entity.Sku, entity.Name, entity.CategoryId, entity.UnitPrice, entity.TaxCategory);

    public static ProductEntity ToEntity(Product product)
    {
        var entity = new ProductEntity { Sku = product.Sku };
        Apply(product, entity);
        return entity;
    }

    public static void Apply(Product product, ProductEntity entity)
    {
        entity.Name = product.Name;
        entity.CategoryId = product.Category;
        entity.UnitPrice = product.UnitPrice;
        entity.TaxCategory = product.TaxCategory;
    }
}

internal static class StockMapper
{
    public static StockItem ToDomain(BranchStockEntity entity) =>
        new(entity.BranchCode, entity.Sku, entity.Quantity);

    public static BranchStockEntity ToEntity(StockItem item) => new()
    {
        BranchCode = item.BranchCode,
        Sku = item.Sku,
        Quantity = item.Quantity
    };

    public static void Apply(StockItem item, BranchStockEntity entity) => entity.Quantity = item.Quantity;

    /// <summary>The domain key looks like "BR001:PKG-0001".</summary>
    public static (BranchCode Branch, Sku Sku) ParseKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var parts = key.Split(':', 2);
        if (parts.Length != 2)
            throw new ArgumentException("A stock key must look like BR001:PKG-0001.", nameof(key));

        return (new BranchCode(parts[0]), new Sku(parts[1]));
    }
}

internal static class ZoneMapper
{
    public static ShippingZone ToDomain(ShippingZoneEntity entity)
    {
        var rate = entity.Rate
            ?? throw new InvalidDataException($"Shipping zone '{entity.Code}' has no rate row.");

        return new ShippingZone(entity.Code, entity.Name, rate.BaseRate, rate.PerKgRate);
    }

    public static ShippingZoneEntity ToEntity(ShippingZone zone)
    {
        var entity = new ShippingZoneEntity { Code = zone.Code };
        Apply(zone, entity);
        return entity;
    }

    public static void Apply(ShippingZone zone, ShippingZoneEntity entity)
    {
        entity.Name = zone.Name;

        if (entity.Rate is null)
            entity.Rate = new ShippingRateEntity { ZoneCode = zone.Code, BaseRate = zone.BaseRate, PerKgRate = zone.PerKgRate };
        else
        {
            entity.Rate.BaseRate = zone.BaseRate;
            entity.Rate.PerKgRate = zone.PerKgRate;
        }
    }
}

internal static class TransactionMapper
{
    private const string RetailType = "Retail";
    private const string ShippingType = "Shipping";
    private const string MoneyOrderType = "MoneyOrder";

    public static CounterTransaction ToDomain(CounterTransactionEntity entity)
    {
        var lines = entity.Lines.OrderBy(line => line.LineNumber).Select(ToLine).ToList();

        return CounterTransaction.Restore(
            entity.Id,
            entity.BranchCode,
            entity.CounterId,
            entity.ClerkId,
            DateMapping.AsUtc(entity.OpenedAtUtc),
            entity.Status,
            entity.Payment?.Method,
            entity.Verification is null ? null : ToVerification(entity.Verification),
            entity.ReversalReason,
            entity.TaxTotal,
            lines);
    }

    public static CounterTransactionEntity ToEntity(CounterTransaction transaction)
    {
        var entity = new CounterTransactionEntity
        {
            Id = transaction.Id,
            BranchCode = transaction.BranchCode,
            CounterId = transaction.CounterId,
            ClerkId = transaction.ClerkId,
            OpenedAtUtc = transaction.CreatedAt.UtcDateTime
        };

        Apply(transaction, entity);
        return entity;
    }

    /// <summary>
    /// Lines are only ever appended while a transaction is open, so rows that already exist never change.
    /// Only the lines after the stored ones are added.
    /// </summary>
    public static void Apply(CounterTransaction transaction, CounterTransactionEntity entity)
    {
        entity.Status = transaction.Status;
        entity.TaxTotal = transaction.TaxTotal;
        entity.ReversalReason = transaction.ReversalReason;

        for (var index = entity.Lines.Count; index < transaction.Lines.Count; index++)
            entity.Lines.Add(ToLineEntity(transaction.Id, index + 1, transaction.Lines[index]));

        if (transaction.PaidWith is { } method)
        {
            if (entity.Payment is null)
                entity.Payment = new PaymentEntity { TransactionId = transaction.Id, Method = method, Amount = transaction.Total };
            else
            {
                entity.Payment.Method = method;
                entity.Payment.Amount = transaction.Total;
            }
        }

        if (transaction.Verification is { } verification)
        {
            if (entity.Verification is null)
                entity.Verification = new IdentityVerificationEntity { TransactionId = transaction.Id };

            entity.Verification.DocumentType = verification.DocumentType;
            entity.Verification.MaskedNumber = verification.MaskedNumber;
            entity.Verification.Result = verification.Result;
            entity.Verification.VerifiedAtUtc = verification.VerifiedAt.UtcDateTime;
        }
    }

    private static TransactionLineEntity ToLineEntity(Guid transactionId, int lineNumber, ILineItem line)
    {
        var entity = new TransactionLineEntity
        {
            TransactionId = transactionId,
            LineNumber = lineNumber,
            Description = line.Description,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            TaxCategory = line.TaxCategory
        };

        switch (line)
        {
            case RetailLineItem retail:
                entity.LineType = RetailType;
                entity.Sku = retail.Sku;
                break;

            case ShippingLineItem shipping:
                entity.LineType = ShippingType;
                entity.Shipment = new ShipmentEntity
                {
                    TransactionId = transactionId,
                    LineNumber = lineNumber,
                    ShipmentType = shipping.ShipmentType,
                    Weight = shipping.Weight,
                    DestinationZone = shipping.DestinationZone,
                    ServiceLevel = shipping.ServiceLevel
                };
                break;

            case MoneyOrderLineItem order:
                entity.LineType = MoneyOrderType;
                entity.MoneyOrder = new MoneyOrderEntity
                {
                    TransactionId = transactionId,
                    LineNumber = lineNumber,
                    Amount = order.Amount,
                    Fee = order.Fee,
                    Sender = order.Sender,
                    Recipient = order.Recipient
                };
                break;

            default:
                throw new NotSupportedException($"Line item type {line.GetType().Name} cannot be stored.");
        }

        return entity;
    }

    private static ILineItem ToLine(TransactionLineEntity line) => line.LineType switch
    {
        RetailType => new RetailLineItem(
            line.Sku ?? throw Missing(line, "Sku"),
            line.Description ?? throw Missing(line, "Description"),
            line.Quantity,
            line.UnitPrice,
            line.TaxCategory),

        ShippingType => ToShipping(line),
        MoneyOrderType => ToMoneyOrder(line),

        _ => throw new InvalidDataException($"Unknown stored line type '{line.LineType}'.")
    };

    private static ShippingLineItem ToShipping(TransactionLineEntity line)
    {
        var shipment = line.Shipment ?? throw Missing(line, "Shipment");
        return new ShippingLineItem(
            shipment.ShipmentType, shipment.Weight, shipment.DestinationZone, shipment.ServiceLevel, line.UnitPrice);
    }

    private static MoneyOrderLineItem ToMoneyOrder(TransactionLineEntity line)
    {
        var order = line.MoneyOrder ?? throw Missing(line, "MoneyOrder");
        return new MoneyOrderLineItem(order.Amount, order.Fee, order.Sender, order.Recipient);
    }

    private static IdentityVerification ToVerification(IdentityVerificationEntity entity)
    {
        // Only the masked value is stored. Masking an already masked value gives the same text, so Create can be
        // reused. The check below catches a damaged row.
        var restored = IdentityVerification.Create(
            entity.DocumentType, entity.MaskedNumber, entity.Result, DateMapping.AsUtc(entity.VerifiedAtUtc));

        if (restored.MaskedNumber != entity.MaskedNumber)
            throw new InvalidDataException("A stored identity verification has an invalid masked number.");

        return restored;
    }

    private static InvalidDataException Missing(TransactionLineEntity line, string field) =>
        new($"A stored {line.LineType} line (number {line.LineNumber}) is missing '{field}'.");
}