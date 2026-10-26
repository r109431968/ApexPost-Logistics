using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApexPost.Web.Health;

/// <summary>Healthy when the branch data file can be read.</summary>
public sealed class DataStoreHealthCheck(IRepository<Branch, BranchCode> branches) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var all = await branches.ListAsync(cancellationToken);
            return HealthCheckResult.Healthy($"{all.Count} branches readable.");
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
