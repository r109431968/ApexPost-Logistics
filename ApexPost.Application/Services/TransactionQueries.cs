using ApexPost.Application.Abstractions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class TransactionQueries(IRepository<CounterTransaction, Guid> transactions) : ITransactionQueries
{
    public Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        transactions.GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<CounterTransaction>> RecentAsync(
        BranchCode branch, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var all = await transactions.ListAsync(TransactionSpecs.ForBranch(branch), cancellationToken);
        return all.OrderByDescending(transaction => transaction.CreatedAt).Take(count).ToList();
    }
}
