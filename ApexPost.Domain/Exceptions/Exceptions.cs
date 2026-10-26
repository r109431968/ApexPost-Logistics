using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Exceptions;

/// <summary>Raised when a business rule is broken.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(Sku sku, int available, int requested)
        : base($"Insufficient stock for {sku}: available {available}, requested {requested}.")
    {
    }
}
