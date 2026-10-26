namespace ApexPost.Domain.ValueObjects;

/// <summary>Format: BR followed by three digits, for example BR001.</summary>
public sealed record BranchCode
{
    public string Value { get; }

    public BranchCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var code = value.Trim().ToUpperInvariant();

        var isValid = code.Length == 5
            && code.StartsWith("BR", StringComparison.Ordinal)
            && code[2..].All(char.IsAsciiDigit);

        if (!isValid)
            throw new ArgumentException("Branch code must look like BR001.", nameof(value));

        Value = code;
    }

    public override string ToString() => Value;
}
