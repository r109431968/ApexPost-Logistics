using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Extensions;

public static class MoneyExtensions
{
    extension(Money money)
    {
        public bool IsPositive => money.Amount > 0m;

        /// <summary>Cash is paid in 5-cent steps, so cash totals round to the nearest 0.05.</summary>
        public Money RoundedForCash()
        {
            var rounded = decimal.Round(money.Amount / 0.05m, 0, MidpointRounding.AwayFromZero) * 0.05m;
            return new Money(rounded, money.Currency);
        }

        public Money Percent(decimal rate) => new(money.Amount * rate, money.Currency);
    }

    extension(IEnumerable<Money> values)
    {
        public Money Total() => values.Aggregate(Money.Zero, (sum, value) => sum + value);
    }
}
