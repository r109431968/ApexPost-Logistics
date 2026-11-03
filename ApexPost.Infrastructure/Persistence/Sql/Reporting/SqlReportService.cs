using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ApexPost.Infrastructure.Persistence.Sql.Reporting;

/// <summary>
/// The report service used by the web application. Every report runs inside SQL Server, so only the small result set
/// travels to the application, never every transaction with its lines:
///   - daily sales and low stock call stored procedures;
///   - shipping volume, the money order audit and the dashboard alerts are set-based SELECT statements that return one
///     row per group (or per money order), not one row per transaction.
/// The dashboard (inherited) calls the overrides. The in-memory base class stays the reference implementation, and the
/// integration tests compare both.
///
/// Values reach SQL Server as parameters: EF Core turns the interpolated strings of the procedure calls into
/// SqlParameter objects (@p0, @p1, ...), and the aggregate queries use explicit, typed SqlParameter objects, so
/// nothing a user typed is ever concatenated into SQL text.
/// </summary>
public sealed class SqlReportService : ReportService
{
    private readonly ApexPostDbContext db;
    private readonly int defaultThreshold;
    private readonly decimal highValueAlertThreshold;

    // Paid = 2, Voided = 3, Refunded = 4 (the CHECK constraint on CounterTransactions.Status). Days are UTC days.
    private const string ShippingVolumeSql = """
        SELECT s.DestinationZone AS ZoneCode,
               s.ServiceLevel    AS ServiceLevel,
               COUNT(*)          AS ShipmentCount,
               SUM(s.WeightGrams) AS TotalGrams,
               SUM(l.UnitPrice)  AS Revenue
        FROM Shipments AS s
        JOIN TransactionLines    AS l ON l.TransactionId = s.TransactionId AND l.LineNumber = s.LineNumber
        JOIN CounterTransactions AS t ON t.Id = s.TransactionId
        WHERE t.Status = 2
          AND t.OpenedAtUtc >= @FromUtc
          AND t.OpenedAtUtc <  @ToUtcExclusive
          AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
        GROUP BY s.DestinationZone, s.ServiceLevel
        ORDER BY s.DestinationZone, s.ServiceLevel
        """;

    private const string MoneyOrderAuditSql = """
        SELECT t.Id                AS TransactionId,
               t.BranchCode        AS BranchCode,
               t.OpenedAtUtc       AS OpenedAtUtc,
               t.Status            AS Status,
               mo.Amount           AS Amount,
               mo.Fee              AS Fee,
               iv.DocumentType     AS DocumentType,
               iv.MaskedNumber     AS MaskedNumber,
               iv.Result           AS VerificationResult
        FROM MoneyOrders AS mo
        JOIN CounterTransactions AS t ON t.Id = mo.TransactionId
        LEFT JOIN IdentityVerifications AS iv ON iv.TransactionId = t.Id
        WHERE (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
        ORDER BY t.BranchCode, t.OpenedAtUtc, t.Id, mo.LineNumber
        """;

    private const string VoidOrRefundAlertsSql = """
        SELECT t.Id AS TransactionId, t.Status AS Status, t.ReversalReason AS ReversalReason
        FROM CounterTransactions AS t
        WHERE t.BranchCode = @BranchCode
          AND t.OpenedAtUtc >= @FromUtc
          AND t.OpenedAtUtc <  @ToUtcExclusive
          AND t.Status IN (3, 4)
        """;

    private const string HighValueMoneyOrderAlertsSql = """
        SELECT t.Id AS TransactionId, mo.Amount AS Amount
        FROM MoneyOrders AS mo
        JOIN CounterTransactions AS t ON t.Id = mo.TransactionId
        WHERE t.BranchCode = @BranchCode
          AND t.OpenedAtUtc >= @FromUtc
          AND t.OpenedAtUtc <  @ToUtcExclusive
          AND t.Status = 2
          AND mo.Amount >= @Threshold
        """;

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
        highValueAlertThreshold = moneyOrderSettings.HighValueAlertThreshold;
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

        // The procedure refuses a negative threshold (THROW 50022). Nothing can be that low, so the answer is an empty list,
        // the same as the in-memory reference gives.
        if (limit < 0)
            return [];

        // EXEC dbo.usp_LowStockReport @BranchCode = @p0, @Threshold = @p1   (@CategoryId keeps its default NULL)
        var rows = await db.Database
            .SqlQuery<LowStockRow>($"EXEC dbo.usp_LowStockReport @BranchCode = {branch.Value}, @Threshold = {limit}")
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new LowStockItem(branch, new Sku(row.Sku), row.Product, row.Quantity, limit))
            .ToList();
    }

    public override async Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        DateOnly fromDay, DateOnly toDay, BranchCode? branch, CancellationToken cancellationToken)
    {
        if (toDay < fromDay)
            throw new ArgumentException("The end day cannot be before the start day.", nameof(toDay));

        var rows = await db.Database
            .SqlQueryRaw<ShippingVolumeSqlRow>(
                ShippingVolumeSql,
                DateTimeParameter("@FromUtc", fromDay),
                DateTimeParameter("@ToUtcExclusive", toDay.AddDays(1)),
                BranchParameter(branch))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ShippingVolumeRow(
                row.ZoneCode,
                (ServiceLevel)row.ServiceLevel,
                row.ShipmentCount,
                row.TotalGrams / 1000m,
                new Money(row.Revenue)))
            .ToList();
    }

    public override async Task<IReadOnlyList<BranchMoneyOrderSummary>> MoneyOrderAuditAsync(
        BranchCode? branch, CancellationToken cancellationToken)
    {
        // One row per money order (with its transaction and optional verification). The totals are then built from
        // these rows only, so the data that travels is the money orders, not every transaction.
        var rows = await db.Database
            .SqlQueryRaw<MoneyOrderSqlRow>(MoneyOrderAuditSql, BranchParameter(branch))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.BranchCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => BuildSummary(new BranchCode(group.Key), group.ToList()))
            .ToList();
    }

    protected override async Task<IReadOnlyList<DashboardAlert>> AlertsAsync(
        BranchCode branch, DateOnly day, CancellationToken cancellationToken)
    {
        // Each command gets its own SqlParameter objects: one SqlParameter cannot belong to two commands.
        var voidsAndRefunds = await db.Database
            .SqlQueryRaw<VoidOrRefundSqlRow>(
                VoidOrRefundAlertsSql,
                BranchParameter(branch),
                DateTimeParameter("@FromUtc", day),
                DateTimeParameter("@ToUtcExclusive", day.AddDays(1)))
            .ToListAsync(cancellationToken);

        var highValue = await db.Database
            .SqlQueryRaw<HighValueSqlRow>(
                HighValueMoneyOrderAlertsSql,
                BranchParameter(branch),
                DateTimeParameter("@FromUtc", day),
                DateTimeParameter("@ToUtcExclusive", day.AddDays(1)),
                new SqlParameter("@Threshold", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = highValueAlertThreshold })
            .ToListAsync(cancellationToken);

        var alerts = new List<DashboardAlert>();

        foreach (var row in voidsAndRefunds)
            alerts.Add(new DashboardAlert(
                "VoidOrRefund", $"{(TransactionStatus)row.Status}: {row.ReversalReason}", row.TransactionId));

        foreach (var row in highValue)
            alerts.Add(new DashboardAlert(
                "HighValueMoneyOrder", $"Money order of {new Money(row.Amount)}", row.TransactionId));

        return alerts;
    }

    private static BranchMoneyOrderSummary BuildSummary(BranchCode code, IReadOnlyList<MoneyOrderSqlRow> rows)
    {
        var auditRows = rows
            .Select(row => new MoneyOrderAuditRow(
                row.TransactionId,
                new DateTimeOffset(DateTime.SpecifyKind(row.OpenedAtUtc, DateTimeKind.Utc)),
                (TransactionStatus)row.Status,
                new Money(row.Amount),
                new Money(row.Fee),
                row.DocumentType is { } document ? (DocumentType)document : null,
                row.MaskedNumber,
                row.VerificationResult is { } result ? (VerificationResult)result : null))
            .ToList();

        var paid = rows.Where(row => row.Status == (int)TransactionStatus.Paid).ToList();

        // A transaction with two money orders appears in two rows but has one verification, so count transactions.
        var verified = rows.Where(row => row.VerificationResult is not null).DistinctBy(row => row.TransactionId).ToList();

        return new BranchMoneyOrderSummary(
            code,
            paid.Count,
            new Money(paid.Sum(row => row.Amount)),
            verified.Count(row => row.VerificationResult == (int)VerificationResult.Passed),
            verified.Count(row => row.VerificationResult == (int)VerificationResult.Failed),
            auditRows);
    }

    /// <summary>A day is a UTC day. The value is a datetime2 parameter, never text glued into the SQL.</summary>
    private static SqlParameter DateTimeParameter(string name, DateOnly day) =>
        new(name, SqlDbType.DateTime2) { Value = day.ToDateTime(TimeOnly.MinValue) };

    /// <summary>NULL means "all branches". The type is stated so the NULL still compares as varchar(5).</summary>
    private static SqlParameter BranchParameter(BranchCode? branch) =>
        new("@BranchCode", SqlDbType.VarChar, 5) { Value = (object?)branch?.Value ?? DBNull.Value };
}

internal sealed class ShippingVolumeSqlRow
{
    public string ZoneCode { get; set; } = string.Empty;
    public int ServiceLevel { get; set; }
    public int ShipmentCount { get; set; }
    public decimal TotalGrams { get; set; }
    public decimal Revenue { get; set; }
}

internal sealed class MoneyOrderSqlRow
{
    public Guid TransactionId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public int Status { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public int? DocumentType { get; set; }
    public string? MaskedNumber { get; set; }
    public int? VerificationResult { get; set; }
}

internal sealed class VoidOrRefundSqlRow
{
    public Guid TransactionId { get; set; }
    public int Status { get; set; }
    public string? ReversalReason { get; set; }
}

internal sealed class HighValueSqlRow
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
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
