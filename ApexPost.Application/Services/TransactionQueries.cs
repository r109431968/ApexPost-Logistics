using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

/// <summary>Read-only queries for the counter screens. Nothing here is tracked and nothing here saves.</summary>
public sealed class TransactionQueries(ICounterTransactionRepository transactions) : ITransactionQueries
{
    public Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        transactions.GetForViewAsync(id, cancellationToken);

    public Task<IReadOnlyList<TransactionListItem>> RecentAsync(
        BranchCode branch, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        // The database sorts, limits and totals, so the list needs one small row per transaction and no aggregate.
        return transactions.RecentSummariesAsync(branch, count, cancellationToken);
    }
}