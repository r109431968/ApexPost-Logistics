using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Reporting;

/// <summary>
/// The report service used by the web application. The daily sales and the low-stock reports run inside SQL Server as
/// stored procedures, so only the small result set travels to the application, not every transaction of the branch.
/// The dashboard (inherited) calls these two overrides. The other reports keep the base implementation.
///
/// Values reach SQL Server as parameters: EF Core turns the interpolated string into SqlParameter objects
/// (@p0, @p1, ...), so nothing a user typed is ever concatenated into SQL text.
/// </summary>
public sealed class SqlReportService : ReportService
{
    private readonly ApexPostDbContext db;
    private readonly int defaultThreshold;

    public SqlReportService(
        IRepository<CounterTransaction, Guid> transactionRepo,
        IRepository<StockItem, string> stockRepo,
        IRepository<Product, Sku> productRepo,
        StockSettings stockSettings,
        MoneyOrderSettings moneyOrderSettings,
        IClock clock,
        ApexPostDbContext db)
        : base(transactionRepo, stockRepo, productRepo, stockSettings, moneyOrderSettings, clock)
    {
        this.db = db;
        defaultThreshold = stockSettings.LowStockThreshold;
    }

    public override async Task<DailySalesReport> DailySalesAsync(
        BranchCode branch, DateOnly day, CancellationToken cancellationToken)
    {
        // EXEC dbo.usp_DailyBranchSales @Day = @p0, @BranchCode = @p1
        var rows = await db.Database
            .SqlQuery<DailySalesRow>($"EXEC dbo.usp_DailyBranchSales @Day = {day}, @BranchCode = {branch.Value}")
            .ToListAsync(cancellationToken);

        // The procedure returns no row for a branch without a paid sale on that day.
        var row = rows.SingleOrDefault();

        return new DailySalesReport(
            branch,
            day,
            row?.Transactions ?? 0,
            new Money(row?.RetailSales ?? 0m),
            new Money(row?.ServiceSales ?? 0m));
    }

    public override async Task<IReadOnlyList<LowStockItem>> LowStockAsync(
        BranchCode branch, int? threshold, CancellationToken cancellationToken)
    {
        var limit = threshold ?? defaultThreshold;

        // EXEC dbo.usp_LowStockReport @BranchCode = @p0, @Threshold = @p1   (@CategoryId keeps its default NULL)
        var rows = await db.Database
            .SqlQuery<LowStockRow>($"EXEC dbo.usp_LowStockReport @BranchCode = {branch.Value}, @Threshold = {limit}")
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new LowStockItem(branch, new Sku(row.Sku), row.Product, row.Quantity, limit))
            .ToList();
    }
}

/// <summary>Shape of the result set of usp_DailyBranchSales. The property names match the column names.</summary>
internal sealed class DailySalesRow
{
    public decimal RetailSales { get; set; }
    public decimal ServiceSales { get; set; }
    public int Transactions { get; set; }
}

/// <summary>Shape of the result set of usp_LowStockReport.</summary>
internal sealed class LowStockRow
{
    public string Sku { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
