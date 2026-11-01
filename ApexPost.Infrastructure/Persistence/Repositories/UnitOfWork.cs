using ApexPost.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// The only place that commits. It shares the request's DbContext with every repository, so one SaveChanges
/// writes everything the use case staged: sale, lines, payment and stock together, or nothing at all.
/// </summary>
public sealed class UnitOfWork(ApexPostDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        // The execution strategy owns retries. The whole block, including the transaction, is the unit it can repeat.
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}