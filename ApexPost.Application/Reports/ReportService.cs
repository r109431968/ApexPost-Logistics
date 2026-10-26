using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Settings;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Reports;

public interface IReportService
{
    Task<DailySalesReport> DailySalesAsync(BranchCode branch, DateOnly day, CancellationToken cancellationToken);

    Task<IReadOnlyList<LowStockItem>> LowStockAsync(
        BranchCode branch, int? threshold, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        DateOnly fromDay, DateOnly toDay, BranchCode? branch, CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchMoneyOrderSummary>> MoneyOrderAuditAsync(
        BranchCode? branch, CancellationToken cancellationToken);

    Task<BranchDashboard> DashboardAsync(BranchCode branch, CancellationToken cancellationToken);
}

public sealed class ReportService(
    IRepository<CounterTransaction, Guid> transactionRepo,
    IRepository<StockItem, string> stockRepo,
    IRepository<Product, Sku> productRepo,
    StockSettings stockSettings,
    MoneyOrderSettings moneyOrderSettings,
    IClock clock) : IReportService
{
    /// <summary>Sales count only Paid transactions. Services = shipping plus money order fees (not the order amount).</summary>
    public async Task<DailySalesReport> DailySalesAsync(
        BranchCode branch, DateOnly day, CancellationToken cancellationToken)
    {
        var paid = await transactionRepo.ListAsync(TransactionSpecs.PaidOnDay(branch, day), cancellationToken);

        var retail = Money.Zero;
        var services = Money.Zero;

        foreach (var transaction in paid)
        {
            foreach (var line in transaction.Lines)
            {
                switch (line)
                {
                    case RetailLineItem:
                        retail += line.LineTotal;
                        break;
                    case ShippingLineItem:
                        services += line.LineTotal;
                        break;
                    case MoneyOrderLineItem order:
                        services += order.Fee;
                        break;
                }
            }
        }

        return new DailySalesReport(branch, day, paid.Count, retail, services);
    }

    public async Task<IReadOnlyList<LowStockItem>> LowStockAsync(
        BranchCode branch, int? threshold, CancellationToken cancellationToken)
    {
        var limit = threshold ?? stockSettings.LowStockThreshold;

        var items = await stockRepo.ListAsync(StockSpecs.LowStock(branch, limit), cancellationToken);
        var products = await productRepo.ListAsync(cancellationToken);
        var names = products.ToDictionary(product => product.Sku, product => product.Name);

        var result = new List<LowStockItem>();
        foreach (var item in items.OrderBy(i => i.Quantity).ThenBy(i => i.Sku.Value, StringComparer.Ordinal))
        {
            var name = names.TryGetValue(item.Sku, out var found) ? found : "(unknown product)";
            result.Add(new LowStockItem(item.BranchCode, item.Sku, name, item.Quantity, limit));
        }

        return result;
    }

    public async Task<IReadOnlyList<ShippingVolumeRow>> ShippingVolumeAsync(
        DateOnly fromDay, DateOnly toDay, BranchCode? branch, CancellationToken cancellationToken)
    {
        if (toDay < fromDay)
            throw new ArgumentException("The end day cannot be before the start day.", nameof(toDay));

        var paid = await transactionRepo.ListAsync(
            TransactionSpecs.PaidBetween(fromDay, toDay, branch), cancellationToken);

        return paid
            .SelectMany(transaction => transaction.Lines.OfType<ShippingLineItem>())
            .GroupBy(shipment => (shipment.DestinationZone, shipment.ServiceLevel))
            .Select(group => new ShippingVolumeRow(
                group.Key.DestinationZone,
                group.Key.ServiceLevel,
                group.Count(),
                group.Sum(shipment => shipment.Weight.Kilograms),
                SumMoney(group.Select(shipment => shipment.UnitPrice))))
            .OrderBy(row => row.ZoneCode, StringComparer.Ordinal)
            .ThenBy(row => row.ServiceLevel)
            .ToList();
    }

    public async Task<IReadOnlyList<BranchMoneyOrderSummary>> MoneyOrderAuditAsync(
        BranchCode? branch, CancellationToken cancellationToken)
    {
        var withOrders = await transactionRepo.ListAsync(TransactionSpecs.WithMoneyOrders(branch), cancellationToken);

        return withOrders
            .GroupBy(transaction => transaction.BranchCode)
            .OrderBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(group => BuildSummary(group.Key, group))
            .ToList();
    }

    /// <summary>The three pieces are independent, so they run in parallel with Task.WhenAll.</summary>
    public async Task<BranchDashboard> DashboardAsync(BranchCode branch, CancellationToken cancellationToken)
    {
        var today = TransactionSpecs.DayOf(clock.UtcNow);

        var salesTask = DailySalesAsync(branch, today, cancellationToken);
        var stockTask = LowStockAsync(branch, null, cancellationToken);
        var alertsTask = AlertsAsync(branch, today, cancellationToken);

        await Task.WhenAll(salesTask, stockTask, alertsTask);

        return new BranchDashboard(branch, today, await salesTask, await stockTask, await alertsTask);
    }

    private async Task<IReadOnlyList<DashboardAlert>> AlertsAsync(
        BranchCode branch, DateOnly day, CancellationToken cancellationToken)
    {
        var todays = await transactionRepo.ListAsync(TransactionSpecs.CreatedOnDay(branch, day), cancellationToken);
        var alerts = new List<DashboardAlert>();

        foreach (var transaction in todays)
        {
            if (transaction.Status is TransactionStatus.Voided or TransactionStatus.Refunded)
            {
                alerts.Add(new DashboardAlert(
                    "VoidOrRefund", $"{transaction.Status}: {transaction.ReversalReason}", transaction.Id));
            }

            if (transaction.Status != TransactionStatus.Paid)
                continue;

            foreach (var order in transaction.Lines.OfType<MoneyOrderLineItem>())
            {
                if (order.Amount.Amount >= moneyOrderSettings.HighValueAlertThreshold)
                    alerts.Add(new DashboardAlert(
                        "HighValueMoneyOrder", $"Money order of {order.Amount}", transaction.Id));
            }
        }

        return alerts;
    }

    private static BranchMoneyOrderSummary BuildSummary(
        BranchCode code, IEnumerable<CounterTransaction> transactions)
    {
        var list = transactions.OrderBy(transaction => transaction.CreatedAt).ToList();
        var rows = new List<MoneyOrderAuditRow>();

        foreach (var transaction in list)
        {
            foreach (var order in transaction.Lines.OfType<MoneyOrderLineItem>())
            {
                rows.Add(new MoneyOrderAuditRow(
                    transaction.Id,
                    transaction.CreatedAt,
                    transaction.Status,
                    order.Amount,
                    order.Fee,
                    transaction.Verification?.DocumentType,
                    transaction.Verification?.MaskedNumber,
                    transaction.Verification?.Result));
            }
        }

        var paidRows = rows.Where(row => row.Status == TransactionStatus.Paid).ToList();

        return new BranchMoneyOrderSummary(
            code,
            paidRows.Count,
            SumMoney(paidRows.Select(row => row.Amount)),
            list.Count(t => t.Verification is { Result: VerificationResult.Passed }),
            list.Count(t => t.Verification is { Result: VerificationResult.Failed }),
            rows);
    }

    private static Money SumMoney(IEnumerable<Money> values) =>
        values.Aggregate(Money.Zero, (sum, value) => sum + value);
}
