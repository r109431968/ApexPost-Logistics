namespace ApexPost.Infrastructure.Seeding;

public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many historical transactions the demo seed creates. The lab needs at least 200 and the default is 240.
    /// Integration tests use a smaller number so each disposable test database is ready quickly.
    /// It must be above 31, because the seed places special cases (large money orders, a failed check) at fixed positions.
    /// </summary>
    public int TransactionCount { get; set; } = 240;

    /// <summary>
    /// Password given to every seeded demo account. It is read from configuration (appsettings.Development.json
    /// or user-secrets). When it is empty no demo account can sign in.
    /// </summary>
    public string? DemoPassword { get; set; }
}
