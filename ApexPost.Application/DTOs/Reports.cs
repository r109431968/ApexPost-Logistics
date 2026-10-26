using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.DTOs;

public sealed record DailySalesReport(
    BranchCode BranchCode, DateOnly Day, int TransactionCount, Money RetailTotal, Money ServicesTotal)
{
    public Money Total => RetailTotal + ServicesTotal;
}

public sealed record LowStockItem(
    BranchCode BranchCode, Sku Sku, string ProductName, int Quantity, int Threshold);

public sealed record ShippingVolumeRow(
    string ZoneCode, ServiceLevel ServiceLevel, int ShipmentCount, decimal TotalWeightKg, Money Revenue);

public sealed record MoneyOrderAuditRow(
    Guid TransactionId,
    DateTimeOffset At,
    TransactionStatus Status,
    Money Amount,
    Money Fee,
    DocumentType? DocumentType,
    string? MaskedDocumentNumber,
    VerificationResult? VerificationResult);

public sealed record BranchMoneyOrderSummary(
    BranchCode BranchCode,
    int PaidMoneyOrderCount,
    Money PaidTotalAmount,
    int VerificationsPassed,
    int VerificationsFailed,
    IReadOnlyList<MoneyOrderAuditRow> Rows);

public sealed record DashboardAlert(string Kind, string Message, Guid TransactionId);

public sealed record BranchDashboard(
    BranchCode BranchCode,
    DateOnly Day,
    DailySalesReport Sales,
    IReadOnlyList<LowStockItem> LowStock,
    IReadOnlyList<DashboardAlert> Alerts);
