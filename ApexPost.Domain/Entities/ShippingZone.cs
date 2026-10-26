using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class ShippingZone : Entity<string>
{
    public string Code => Id;
    public string Name { get; private set; }
    public Money BaseRate { get; private set; }
    public Money PerKgRate { get; private set; }

    public ShippingZone(string code, string name, Money baseRate, Money perKgRate)
        : base(NormaliseCode(code))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        BaseRate = baseRate;
        PerKgRate = perKgRate;
    }

    public void ChangeRates(Money baseRate, Money perKgRate)
    {
        BaseRate = baseRate;
        PerKgRate = perKgRate;
    }

    private static string NormaliseCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code.Trim().ToUpperInvariant();
    }
}
