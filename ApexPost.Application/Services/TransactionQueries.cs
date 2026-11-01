using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class TransactionQueries(ICounterTransactionRepository transactions) : ITransactionQueries
{
    public Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        transactions.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<CounterTransaction>> RecentAsync(
        BranchCode branch, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        // The database sorts and limits, so only the newest rows are loaded.
        return transactions.RecentForBranchAsync(branch, count, cancellationToken);
    }
}