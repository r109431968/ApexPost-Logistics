using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;

namespace ApexPost.Application.Services;

public sealed class ReversalService(
    IRepository<Staff, Guid> staff,
    IRepository<StockItem, string> stock,
    IRepository<CounterTransaction, Guid> transactions,
    CounterEvents events,
    IClock clock) : IReversalService
{
    public Task<CounterTransaction> VoidAsync(
        Guid transactionId, Guid managerId, string reason, CancellationToken cancellationToken) =>
        ReverseAsync(transactionId, managerId, reason, isRefund: false, cancellationToken);

    public Task<CounterTransaction> RefundAsync(
        Guid transactionId, Guid managerId, string reason, CancellationToken cancellationToken) =>
        ReverseAsync(transactionId, managerId, reason, isRefund: true, cancellationToken);

    private async Task<CounterTransaction> ReverseAsync(
        Guid transactionId, Guid managerId, string reason, bool isRefund, CancellationToken cancellationToken)
    {
        await StockGate.Gate.WaitAsync(cancellationToken);
        try
        {
            var transaction = await transactions.GetByIdAsync(transactionId, cancellationToken)
                ?? throw new NotFoundException(nameof(CounterTransaction), transactionId);
            var manager = await staff.GetByIdAsync(managerId, cancellationToken)
                ?? throw new NotFoundException(nameof(Staff), managerId);

            var stockWasTaken = transaction.Status == TransactionStatus.Paid;

            // The Domain enforces: Branch Manager role, same branch, reason required, valid status.
            if (isRefund)
                transaction.Refund(manager, reason);
            else
                transaction.Void(manager, reason);

            if (stockWasTaken)
                await RestockAsync(transaction, cancellationToken);

            await transactions.UpdateAsync(transaction, cancellationToken);

            events.Publish(new TransactionVoidedEvent(
                transaction.Id, transaction.BranchCode, manager.Id,
                transaction.ReversalReason ?? reason, isRefund, clock.UtcNow));

            return transaction;
        }
        finally
        {
            StockGate.Gate.Release();
        }
    }

    private async Task RestockAsync(CounterTransaction transaction, CancellationToken cancellationToken)
    {
        var returned = transaction.Lines
            .OfType<RetailLineItem>()
            .GroupBy(line => line.Sku)
            .Select(group => (Sku: group.Key, Quantity: group.Sum(line => line.Quantity)));

        foreach (var (sku, quantity) in returned)
        {
            var key = StockItem.KeyFor(transaction.BranchCode, sku);
            var item = await stock.GetByIdAsync(key, cancellationToken);

            if (item is null)
            {
                await stock.AddAsync(new StockItem(transaction.BranchCode, sku, quantity), cancellationToken);
            }
            else
            {
                item.Add(quantity);
                await stock.UpdateAsync(item, cancellationToken);
            }
        }
    }
}
