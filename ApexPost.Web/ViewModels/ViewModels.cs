using System.ComponentModel.DataAnnotations;
using ApexPost.Application.DTOs;

namespace ApexPost.Web.ViewModels;

/// <summary>Display helpers shared by the views.</summary>
public static class Fmt
{
    public static string Price(Money money) => $"{money.Amount:N2} {money.Currency}";

    public static string When(DateTimeOffset moment) => moment.UtcDateTime.ToString("yyyy-MM-dd HH:mm") + " UTC";

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd");
}

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Email is not valid."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required."), DataType(DataType.Password), StringLength(200)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class ErrorViewModel
{
    public int Code { get; init; }
    public string Title { get; init; } = "Something went wrong";
    public string Message { get; init; } = "Please try again.";
    public string TraceId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
}

public sealed class DashboardViewModel
{
    public required BranchDashboard Dashboard { get; init; }
    public required string BranchName { get; init; }
    public required IReadOnlyList<Notification> Notifications { get; init; }
}

public sealed class CatalogueViewModel
{
    public required string BranchCode { get; init; }
    public required IReadOnlyList<CatalogueRow> Rows { get; init; }
    public ProductCategory? Category { get; init; }
    public string? Search { get; init; }
    public bool CanAdjust { get; init; }
}

public sealed class StockAdjustmentViewModel
{
    [Required, RegularExpression(@"^[A-Za-z]{3}-\d{4}$", ErrorMessage = "SKU must look like PKG-0001.")]
    public string Sku { get; set; } = string.Empty;

    [Range(-1000, 1000, ErrorMessage = "Change must be between -1000 and 1000.")]
    public int Delta { get; set; }

    [Required(ErrorMessage = "A reason is required."), StringLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class TransactionListViewModel
{
    public required string BranchCode { get; init; }
    public required IReadOnlyList<CounterTransaction> Transactions { get; init; }
}

public sealed class NewTransactionViewModel
{
    public required Branch Branch { get; init; }
}

public sealed class TransactionDetailsViewModel
{
    public required CounterTransaction Transaction { get; init; }
    public required IReadOnlyList<CatalogueRow> Products { get; init; }
    public required IReadOnlyList<ShippingZone> Zones { get; init; }
    public required decimal VerificationThreshold { get; init; }
    public bool CanReverse { get; init; }
}

public sealed class AddRetailViewModel
{
    [Required, RegularExpression(@"^[A-Za-z]{3}-\d{4}$", ErrorMessage = "Choose a product.")]
    public string Sku { get; set; } = string.Empty;

    [Range(1, 500, ErrorMessage = "Quantity must be between 1 and 500.")]
    public int Quantity { get; set; } = 1;
}

public sealed class AddShippingViewModel
{
    [EnumDataType(typeof(ShipmentType))]
    public ShipmentType ShipmentType { get; set; } = ShipmentType.Parcel;

    [Range(0.001, 100, ErrorMessage = "Weight must be between 0.001 and 100 kg.")]
    public decimal WeightKg { get; set; }

    [Required(ErrorMessage = "Choose a zone."), StringLength(10)]
    public string ZoneCode { get; set; } = string.Empty;

    [EnumDataType(typeof(ServiceLevel))]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;
}

public sealed class AddMoneyOrderViewModel
{
    [Required(ErrorMessage = "Sender is required."), StringLength(80)]
    public string Sender { get; set; } = string.Empty;

    [Required(ErrorMessage = "Recipient is required."), StringLength(80)]
    public string Recipient { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Amount must be between 0.01 and 100000.")]
    public decimal Amount { get; set; }
}

public sealed class VerifyIdentityViewModel
{
    [EnumDataType(typeof(DocumentType))]
    public DocumentType DocumentType { get; set; } = DocumentType.Passport;

    /// <summary>Only used to build the masked value. It is never stored, logged or audited.</summary>
    [Required(ErrorMessage = "Document number is required."), StringLength(40, MinimumLength = 4, ErrorMessage = "Document number must have 4 to 40 characters.")]
    public string DocumentNumber { get; set; } = string.Empty;

    [EnumDataType(typeof(VerificationResult))]
    public VerificationResult Result { get; set; } = VerificationResult.Passed;
}

public sealed class PayViewModel
{
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
}

public sealed class ReasonViewModel
{
    [Required(ErrorMessage = "A reason is required."), StringLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class LabelViewModel
{
    public required CounterTransaction Transaction { get; init; }
    public required ShippingLineItem Line { get; init; }
    public required string FromBranch { get; init; }
    public required string Reference { get; init; }
}

public sealed class StaffFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Name is required."), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Email is not valid."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [EnumDataType(typeof(StaffRole))]
    public StaffRole Role { get; set; } = StaffRole.CounterClerk;

    [Required(ErrorMessage = "Home branch is required."), RegularExpression(@"^[Bb][Rr]\d{3}$", ErrorMessage = "Branch code must look like BR001.")]
    public string HomeBranch { get; set; } = string.Empty;

    /// <summary>Only used when creating an account.</summary>
    [DataType(DataType.Password), StringLength(100)]
    public string? Password { get; set; }
}

public sealed class StaffListViewModel
{
    public required IReadOnlyList<Staff> Staff { get; init; }
    public Guid CurrentStaffId { get; init; }
}

public sealed class ReportFilterViewModel
{
    public required string BranchCode { get; init; }
    public bool CanChooseBranch { get; init; }
}

public sealed class DailySalesViewModel
{
    public required ReportFilterViewModel Filter { get; init; }
    public required DailySalesReport Report { get; init; }
}

public sealed class LowStockViewModel
{
    public required ReportFilterViewModel Filter { get; init; }
    public required IReadOnlyList<LowStockItem> Items { get; init; }
    public int? Threshold { get; init; }
}

public sealed class ShippingVolumeViewModel
{
    public required ReportFilterViewModel Filter { get; init; }
    public required IReadOnlyList<ShippingVolumeRow> Rows { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

public sealed class MoneyOrderAuditViewModel
{
    public required ReportFilterViewModel Filter { get; init; }
    public required IReadOnlyList<BranchMoneyOrderSummary> Summaries { get; init; }
}
