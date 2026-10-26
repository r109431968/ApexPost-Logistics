using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>Gives every staff member without a password the demo password (demo accounts only).</summary>
public sealed class CredentialSeeder(
    SeedSettings settings,
    IRepository<Staff, Guid> staff,
    ICredentialStore credentials,
    IPasswordHasher hasher,
    ILogger<CredentialSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(settings.DemoPassword))
        {
            logger.LogWarning("No demo password is configured (Seed:DemoPassword), so demo accounts cannot sign in.");
            return;
        }

        var created = 0;
        foreach (var member in await staff.ListAsync(cancellationToken))
        {
            if (await credentials.GetHashAsync(member.Id, cancellationToken) is not null)
                continue;

            await credentials.SetHashAsync(member.Id, hasher.Hash(settings.DemoPassword), cancellationToken);
            created++;
        }

        if (created > 0)
            logger.LogInformation("Set the demo password for {Count} staff accounts.", created);
    }
}
