namespace ApexPost.Domain.ValueObjects;

public sealed record Weight
{
    public decimal Grams { get; }
    public decimal Kilograms => Grams / 1000m;

    private Weight(decimal grams) => Grams = grams;

    public static Weight FromGrams(decimal grams)
    {
        if (grams <= 0)
            throw new ArgumentOutOfRangeException(nameof(grams), "Weight must be greater than zero.");
        return new Weight(grams);
    }

    public static Weight FromKilograms(decimal kilograms) => FromGrams(kilograms * 1000m);

    public static bool operator >(Weight left, Weight right) => left.Grams > right.Grams;
    public static bool operator <(Weight left, Weight right) => left.Grams < right.Grams;
    public static bool operator >=(Weight left, Weight right) => left.Grams >= right.Grams;
    public static bool operator <=(Weight left, Weight right) => left.Grams <= right.Grams;

    public override string ToString() => $"{Kilograms:0.###} kg";
}
