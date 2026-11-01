namespace ApexPost.Application.Abstractions;

/// <summary>
/// One commit per use case. Repositories only stage changes; the unit of work writes them all together.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the work inside one explicit database transaction. Use it only when a use case mixes
    /// SaveChanges with a stored procedure or a set-based update.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}