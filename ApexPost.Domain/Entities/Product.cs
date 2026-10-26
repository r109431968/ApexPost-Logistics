using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Product : Entity<Sku>
{
    public Sku Sku => Id;
    public string Name { get; private set; }
    public ProductCategory Category { get; private set; }
    public Money UnitPrice { get; private set; }
    public TaxCategory TaxCategory { get; private set; }

    public Product(Sku sku, string name, ProductCategory category, Money unitPrice, TaxCategory taxCategory)
        : base(sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Category = category;
        UnitPrice = unitPrice;
        TaxCategory = taxCategory;
    }

    public void ChangePrice(Money newPrice) => UnitPrice = newPrice;
}
