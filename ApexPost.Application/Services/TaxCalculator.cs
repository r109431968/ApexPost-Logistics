using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class TaxCalculator(TaxSettings settings) : ITaxCalculator
{
    public Money TaxFor(ILineItem line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var rate = line.TaxCategory switch
        {
            TaxCategory.Standard => settings.StandardRate,
            TaxCategory.Reduced => settings.ReducedRate,
            TaxCategory.Exempt => 0m,
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.TaxCategory, "Unknown tax category.")
        };

        var lineTotal = line.LineTotal;
        return new Money(lineTotal.Amount * rate, lineTotal.Currency);
    }

    /// <summary>The delegate that CounterTransaction.Pay receives.</summary>
    public Func<ILineItem, Money> AsRule() => TaxFor;
}
