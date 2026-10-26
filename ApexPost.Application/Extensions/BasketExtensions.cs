using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Extensions;

public static class BasketExtensions
{
    extension(CounterTransaction transaction)
    {
        public int ItemCount => transaction.Lines.Sum(line => line.Quantity);

        public int ShippingCount => transaction.Lines.OfType<ShippingLineItem>().Count();

        public bool HasMoneyOrder => transaction.Lines.OfType<MoneyOrderLineItem>().Any();

        public Money RetailSubtotal => transaction.Lines
            .Where(line => line is RetailLineItem)
            .Select(line => line.LineTotal)
            .Total();

        public Money ServicesSubtotal => transaction.Lines
            .Where(line => line is not RetailLineItem)
            .Select(line => line.LineTotal)
            .Total();
    }
}
