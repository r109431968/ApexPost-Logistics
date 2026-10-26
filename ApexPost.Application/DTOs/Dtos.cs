using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.DTOs;

/// <summary>
/// One audit record. It deliberately has no field for passwords, full ID document numbers,
/// API keys or card data, so those can never be written by mistake.
/// </summary>
public sealed record AuditEntry
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public Guid? StaffId { get; init; }
    public string? Role { get; init; }
    public string? BranchCode { get; init; }
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public string? Operation { get; init; }
    public IReadOnlyDictionary<string, string> RouteValues { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }
    public required string CorrelationId { get; init; }
}

public sealed record TrackingResult(string TrackingNumber, string Status, DateTimeOffset LastUpdated);

public enum NotificationKind
{
    LowStock = 1,
    HighValueMoneyOrder = 2,
    VoidOrRefund = 3
}

public sealed record Notification(NotificationKind Kind, string Title, string Message, DateTimeOffset At);

public sealed record CatalogueRow(
    Sku Sku, string Name, ProductCategory Category, Money UnitPrice, TaxCategory TaxCategory, int Quantity, bool IsLow);
