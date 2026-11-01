using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApexPost.Web.Composition;

/// <summary>
/// Used only by the dotnet-ef tool at design time (migrations, scripts). The running app takes its connection
/// string from user-secrets. This fallback is LocalDB with Windows sign-in, so it holds no password.
/// </summary>
public sealed class ApexPostDbContextFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True";

    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APEXPOST_CONNECTION") ?? LocalDbFallback;
        var options = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connectionString).Options;
        return new ApexPostDbContext(options, new SystemClock());
    }
}