using System.Data;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Sql.Entities;
using ApexPost.Infrastructure.Persistence.Sql.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Repositories;

public sealed class BranchRepository(ApexPostDbContext db)
    : EfRepository<Branch, BranchCode, BranchEntity>(db), IBranchRepository
{
    protected override IQueryable<BranchEntity> Query() => Db.Branches.Include(branch => branch.Counters);

    protected override async Task<BranchEntity?> FindAsync(BranchCode id, CancellationToken cancellationToken) =>
        Db.Branches.Local.FirstOrDefault(branch => branch.Code == id)
        ?? await Query().FirstOrDefaultAsync(branch => branch.Code == id, cancellationToken);

    protected override Branch ToDomain(BranchEntity entity) => BranchMapper.ToDomain(entity);
    protected override BranchEntity ToEntity(Branch domain) => BranchMapper.ToEntity(domain);
    protected override void Apply(Branch domain, BranchEntity entity) => BranchMapper.Apply(domain, entity);
}

public sealed class StaffRepository(ApexPostDbContext db)
    : EfRepository<Staff, Guid, StaffEntity>(db), IStaffRepository
{
    protected override IQueryable<StaffEntity> Query() => Db.Staff;

    protected override async Task<StaffEntity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Db.Staff.Local.FirstOrDefault(member => member.Id == id)
        ?? await Db.Staff.FirstOrDefaultAsync(member => member.Id == id, cancellationToken);

    public async Task<Staff?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var normalised = email.Trim().ToLowerInvariant();
        var entity = await Db.Staff.AsNoTracking()
            .FirstOrDefaultAsync(member => member.Email == normalised, cancellationToken);

        return entity is null ? null : StaffMapper.ToDomain(entity);
    }

    protected override Staff ToDomain(StaffEntity entity) => StaffMapper.ToDomain(entity);
    protected override StaffEntity ToEntity(Staff domain) => StaffMapper.ToEntity(domain);
    protected override void Apply(Staff domain, StaffEntity entity) => StaffMapper.Apply(domain, entity);
}

public sealed class ProductRepository(ApexPostDbContext db)
    : EfRepository<Product, Sku, ProductEntity>(db), IProductRepository
{
    protected override IQueryable<ProductEntity> Query() => Db.Products;

    protected override async Task<ProductEntity?> FindAsync(Sku id, CancellationToken cancellationToken) =>
        Db.Products.Local.FirstOrDefault(product => product.Sku == id)
        ?? await Db.Products.FirstOrDefaultAsync(product => product.Sku == id, cancellationToken);

    protected override Product ToDomain(ProductEntity entity) => ProductMapper.ToDomain(entity);
    protected override ProductEntity ToEntity(Product domain) => ProductMapper.ToEntity(domain);
    protected override void Apply(Product domain, ProductEntity entity) => ProductMapper.Apply(domain, entity);
}

public sealed class ZoneRepository(ApexPostDbContext db)
    : EfRepository<ShippingZone, string, ShippingZoneEntity>(db), IZoneRepository
{
    protected override IQueryable<ShippingZoneEntity> Query() => Db.ShippingZones.Include(zone => zone.Rate);

    protected override async Task<ShippingZoneEntity?> FindAsync(string id, CancellationToken cancellationToken) =>
        Db.ShippingZones.Local.FirstOrDefault(zone => zone.Code == id)
        ?? await Query().FirstOrDefaultAsync(zone => zone.Code == id, cancellationToken);

    protected override ShippingZone ToDomain(ShippingZoneEntity entity) => ZoneMapper.ToDomain(entity);
    protected override ShippingZoneEntity ToEntity(ShippingZone domain) => ZoneMapper.ToEntity(domain);
    protected override void Apply(ShippingZone domain, ShippingZoneEntity entity) => ZoneMapper.Apply(domain, entity);
}

public sealed class StockRepository(ApexPostDbContext db)
    : EfRepository<StockItem, string, BranchStockEntity>(db), IStockRepository
{
    protected override IQueryable<BranchStockEntity> Query() => Db.BranchStock;

    /// <summary>
    /// The tracked row also remembers the rowversion it was read with. If another clerk changed the same row
    /// first, SaveChanges throws DbUpdateConcurrencyException instead of overwriting their change.
    /// </summary>
    protected override async Task<BranchStockEntity?> FindAsync(string id, CancellationToken cancellationToken)
    {
        var (branch, sku) = StockMapper.ParseKey(id);

        return Db.BranchStock.Local.FirstOrDefault(row => row.BranchCode == branch && row.Sku == sku)
               ?? await Db.BranchStock.FirstOrDefaultAsync(
                   row => row.BranchCode == branch && row.Sku == sku, cancellationToken);
    }

    public async Task<IReadOnlyList<StockItem>> ListForBranchAsync(
        BranchCode branch, CancellationToken cancellationToken)
    {
        var rows = await Db.BranchStock.AsNoTracking()
            .Where(row => row.BranchCode == branch)
            .OrderBy(row => row.Sku)
            .ToListAsync(cancellationToken);

        return rows.Select(StockMapper.ToDomain).ToList();
    }

    protected override StockItem ToDomain(BranchStockEntity entity) => StockMapper.ToDomain(entity);
    protected override BranchStockEntity ToEntity(StockItem domain) => StockMapper.ToEntity(domain);
    protected override void Apply(StockItem domain, BranchStockEntity entity) => StockMapper.Apply(domain, entity);
}

public sealed class CounterTransactionRepository(ApexPostDbContext db)
    : EfRepository<CounterTransaction, Guid, CounterTransactionEntity>(db), ICounterTransactionRepository
{
    /// <summary>
    /// One transaction has many lines and each line can have a shipment or money order row. A split query avoids
    /// one huge joined result where the same transaction data repeats for every line.
    /// </summary>
    protected override IQueryable<CounterTransactionEntity> Query() => Db.CounterTransactions
        .Include(transaction => transaction.Lines).ThenInclude(line => line.Shipment)
        .Include(transaction => transaction.Lines).ThenInclude(line => line.MoneyOrder)
        .Include(transaction => transaction.Payment)
        .Include(transaction => transaction.Verification)
        .AsSplitQuery();

    protected override async Task<CounterTransactionEntity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Db.CounterTransactions.Local.FirstOrDefault(transaction => transaction.Id == id)
        ?? await Query().FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CounterTransaction>> RecentForBranchAsync(
        BranchCode branch, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var rows = await Query().AsNoTracking()
            .Where(transaction => transaction.BranchCode == branch)
            .OrderByDescending(transaction => transaction.OpenedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows.Select(TransactionMapper.ToDomain).ToList();
    }

    // Branch, newest first, limited. Total = sum of the line totals (quantity x unit price) + tax, the same rule as
    // CounterTransaction.Total. The values reach SQL Server as typed parameters.
    private const string RecentSummariesSql = """
        SELECT TOP (@Count)
               t.Id AS Id,
               t.OpenedAtUtc AS OpenedAtUtc,
               t.Status AS Status,
               COUNT(l.LineNumber) AS LineCount,
               COALESCE(SUM(l.Quantity * l.UnitPrice), 0) + t.TaxTotal AS Total
        FROM CounterTransactions AS t
        LEFT JOIN TransactionLines AS l ON l.TransactionId = t.Id
        WHERE t.BranchCode = @BranchCode
        GROUP BY t.Id, t.OpenedAtUtc, t.Status, t.TaxTotal
        ORDER BY t.OpenedAtUtc DESC, t.Id
        """;

    public async Task<IReadOnlyList<TransactionListItem>> RecentSummariesAsync(
        BranchCode branch, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var rows = await Db.Database
            .SqlQueryRaw<TransactionListRow>(
                RecentSummariesSql,
                new SqlParameter("@Count", SqlDbType.Int) { Value = count },
                new SqlParameter("@BranchCode", SqlDbType.VarChar, 5) { Value = branch.Value })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new TransactionListItem(
                row.Id,
                new DateTimeOffset(DateTime.SpecifyKind(row.OpenedAtUtc, DateTimeKind.Utc)),
                (TransactionStatus)row.Status,
                row.LineCount,
                new Money(row.Total)))
            .ToList();
    }

    public async Task<CounterTransaction?> GetForViewAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Query().AsNoTracking()
            .FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

        return entity is null ? null : TransactionMapper.ToDomain(entity);
    }

    protected override CounterTransaction ToDomain(CounterTransactionEntity entity) => TransactionMapper.ToDomain(entity);
    protected override CounterTransactionEntity ToEntity(CounterTransaction domain) => TransactionMapper.ToEntity(domain);
    protected override void Apply(CounterTransaction domain, CounterTransactionEntity entity) =>
        TransactionMapper.Apply(domain, entity);
}

/// <summary>Shape of the result set of the transaction list query. The property names match the column names.</summary>
internal sealed class TransactionListRow
{
    public Guid Id { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public int Status { get; set; }
    public int LineCount { get; set; }
    public decimal Total { get; set; }
}
