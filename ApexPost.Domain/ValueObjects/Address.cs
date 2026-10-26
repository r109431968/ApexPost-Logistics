namespace ApexPost.Domain.ValueObjects;

/// <summary>Uses the C# 14 field keyword so each property validates and normalises without a backing field.</summary>
public sealed record Address
{
    public required string Line1
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim();
        }
    }

    public required string City
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim();
        }
    }

    public required string Postcode
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim().ToUpperInvariant();
        }
    }

    public required string Country
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim();
        }
    }

    public override string ToString() => $"{Line1}, {City} {Postcode}, {Country}";
}
