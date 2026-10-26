namespace ApexPost.Domain.ValueObjects;

/// <summary>Format: three letters, a dash and four digits, for example PKG-0001.</summary>
public sealed record Sku
{
    public string Value { get; }

    public Sku(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var sku = value.Trim().ToUpperInvariant();

        var isValid = sku.Length == 8
            && sku[..3].All(char.IsAsciiLetterUpper)
            && sku[3] == '-'
            && sku[4..].All(char.IsAsciiDigit);

        if (!isValid)
            throw new ArgumentException("SKU must look like PKG-0001.", nameof(value));

        Value = sku;
    }

    public override string ToString() => Value;
}
