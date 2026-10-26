using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

/// <summary>Stock is held per branch. Branch stock can never go negative.</summary>
public sealed class StockItem : Entity<string>
{
    public BranchCode BranchCode { get; }
    public Sku Sku { get; }
    public int Quantity { get; private set; }

    public StockItem(BranchCode branchCode, Sku sku, int quantity)
        : base(KeyFor(branchCode, sku))
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        BranchCode = branchCode;
        Sku = sku;
        Quantity = quantity;
    }

    public static string KeyFor(BranchCode branchCode, Sku sku) => $"{branchCode}:{sku}";

    public void Add(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Quantity += quantity;
    }

    public void Remove(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (quantity > Quantity)
            throw new InsufficientStockException(Sku, Quantity, quantity);
        Quantity -= quantity;
    }
}
