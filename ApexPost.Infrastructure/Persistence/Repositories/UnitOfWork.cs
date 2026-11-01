using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// The only place that commits. It shares the request's DbContext with every repository, so one SaveChanges
/// writes everything the use case staged: sale, lines, payment and stock together, or nothing at all.
/// Database errors that the application understands are translated here, so no layer above this one needs to know
/// about Entity Framework or SQL Server error numbers.
/// </summary>
public sealed class UnitOfWork(ApexPostDbContext db) : IUnitOfWork
{
    // SQL Server error numbers for "duplicate key in a unique index" and "violation of a unique constraint".
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        // The execution strategy owns retries. The whole block, including the transaction, is the unit it can repeat.
        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await work(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (Exception exception) when (Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    /// <summary>Returns the application exception for a database error, or null when the error is not one we translate.</summary>
    private static Exception? Translate(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => new ConcurrencyConflictException(exception),
        DbUpdateException { InnerException: SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } }
            => new DuplicateValueException(exception),
        _ => null
    };
}
