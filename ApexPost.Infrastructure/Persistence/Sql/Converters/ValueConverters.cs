using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Sql.Converters;

// Value objects are stored as plain columns. Reading goes back through the constructor,
// so a bad value in the database is rejected by the same rules the domain uses.

public sealed class BranchCodeConverter : ValueConverter<BranchCode, string>
{
    public BranchCodeConverter() : base(code => code.Value, text => new BranchCode(text)) { }
}

public sealed class SkuConverter : ValueConverter<Sku, string>
{
    public SkuConverter() : base(sku => sku.Value, text => new Sku(text)) { }
}

/// <summary>Only the amount is stored. The system uses one currency (USD), so a currency column would repeat a constant.</summary>
public sealed class MoneyConverter : ValueConverter<Money, decimal>
{
    public MoneyConverter() : base(money => money.Amount, amount => new Money(amount, Money.DefaultCurrency)) { }
}

/// <summary>Weight is stored in grams.</summary>
public sealed class WeightConverter : ValueConverter<Weight, decimal>
{
    public WeightConverter() : base(weight => weight.Grams, grams => Weight.FromGrams(grams)) { }
}