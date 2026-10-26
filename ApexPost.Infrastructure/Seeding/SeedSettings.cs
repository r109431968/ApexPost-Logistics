namespace ApexPost.Infrastructure.Seeding;

public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Password given to every seeded demo account. It is read from configuration (appsettings.Development.json
    /// or user-secrets). When it is empty no demo account can sign in.
    /// </summary>
    public string? DemoPassword { get; set; }
}
