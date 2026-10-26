using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Settings;

public sealed class ShippingSettings
{
    public const string SectionName = "Shipping";

    public decimal MaxLetterKg { get; set; } = 2m;
    public decimal MaxStandardParcelKg { get; set; } = 30m;
    public decimal MaxExpressParcelKg { get; set; } = 20m;
    public decimal MaxRegisteredParcelKg { get; set; } = 10m;

    public decimal StandardMultiplier { get; set; } = 1.0m;
    public decimal ExpressMultiplier { get; set; } = 1.6m;
    public decimal RegisteredMultiplier { get; set; } = 1.3m;

    public WeightLimitRule CreateWeightLimitRule() => new(
        Weight.FromKilograms(MaxLetterKg),
        Weight.FromKilograms(MaxStandardParcelKg),
        Weight.FromKilograms(MaxExpressParcelKg),
        Weight.FromKilograms(MaxRegisteredParcelKg));
}

public sealed class TaxSettings
{
    public const string SectionName = "Tax";

    public decimal StandardRate { get; set; } = 0.20m;
    public decimal ReducedRate { get; set; } = 0.05m;
}

public sealed class MoneyOrderSettings
{
    public const string SectionName = "MoneyOrders";

    public decimal IdentityVerificationThreshold { get; set; } = 300m;
    public decimal HighValueAlertThreshold { get; set; } = 1000m;
    public decimal FeePercent { get; set; } = 0.02m;
    public decimal MinimumFee { get; set; } = 2m;
}

public sealed class StockSettings
{
    public const string SectionName = "Stock";

    public int LowStockThreshold { get; set; } = 5;
}

public sealed class PasswordSettings
{
    public const string SectionName = "Passwords";

    /// <summary>PBKDF2-HMAC-SHA256 iterations. Tests lower this to stay fast.</summary>
    public int Iterations { get; set; } = 600_000;
}
