using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

/// <summary>
/// One line of a counter basket. Retail goods and services are different classes that share this
/// interface, so a basket can mix them (composition, not inheritance).
/// </summary>
public interface ILineItem
{
    string Description { get; }
    int Quantity { get; }
    Money UnitPrice { get; }
    TaxCategory TaxCategory { get; }
    Money LineTotal { get; }
}

public sealed class RetailLineItem : ILineItem
{
    public Sku Sku { get; }
    public string Description { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }
    public TaxCategory TaxCategory { get; }
    public Money LineTotal => UnitPrice * Quantity;

    public RetailLineItem(Sku sku, string description, int quantity, Money unitPrice, TaxCategory taxCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Sku = sku;
        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxCategory = taxCategory;
    }
}

public sealed class ShippingLineItem : ILineItem
{
    public ShipmentType ShipmentType { get; }
    public Weight Weight { get; }
    public string DestinationZone { get; }
    public ServiceLevel ServiceLevel { get; }
    public int Quantity => 1;
    public Money UnitPrice { get; }
    public TaxCategory TaxCategory => TaxCategory.Standard;
    public Money LineTotal => UnitPrice * Quantity;
    public string Description => $"{ServiceLevel} {ShipmentType} to zone {DestinationZone} ({Weight})";

    public ShippingLineItem(
        ShipmentType shipmentType, Weight weight, string destinationZone, ServiceLevel serviceLevel, Money price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZone);
        ShipmentType = shipmentType;
        Weight = weight;
        DestinationZone = destinationZone.Trim();
        ServiceLevel = serviceLevel;
        UnitPrice = price;
    }
}

public sealed class MoneyOrderLineItem : ILineItem
{
    public Money Amount { get; }
    public Money Fee { get; }
    public string Sender { get; }
    public string Recipient { get; }
    public int Quantity => 1;
    public Money UnitPrice => Amount + Fee;
    public TaxCategory TaxCategory => TaxCategory.Exempt;
    public Money LineTotal => UnitPrice * Quantity;
    public string Description => $"Money order {Amount} from {Sender} to {Recipient}";

    public MoneyOrderLineItem(Money amount, Money fee, string sender, string recipient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        if (amount.IsZero)
            throw new ArgumentException("Money order amount must be greater than zero.", nameof(amount));

        Amount = amount;
        Fee = fee;
        Sender = sender.Trim();
        Recipient = recipient.Trim();
    }
}
