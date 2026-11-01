using ApexPost.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApexPost.Web.Health;

/// <summary>Healthy when SQL Server answers and the branch table can be read.</summary>
public sealed class DataStoreHealthCheck(ApexPostDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await db.Branches.CountAsync(cancellationToken);
            return HealthCheckResult.Healthy($"{count} branches readable.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The data store cannot be read.", ex);
        }
    }
}