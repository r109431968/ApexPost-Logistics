using ApexPost.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ApexPost.Application.Services;

/// <summary>Plain C# events (the observer pattern) for the four things other parts of the system care about.</summary>
public sealed class CounterEvents(ILogger<CounterEvents> logger)
{
    public event EventHandler<TransactionCompletedEvent>? TransactionCompleted;
    public event EventHandler<StockLowEvent>? StockLow;
    public event EventHandler<MoneyOrderIssuedEvent>? MoneyOrderIssued;
    public event EventHandler<TransactionVoidedEvent>? TransactionVoided;

    public void Publish(TransactionCompletedEvent e) => Invoke(TransactionCompleted, e);

    public void Publish(StockLowEvent e) => Invoke(StockLow, e);

    public void Publish(MoneyOrderIssuedEvent e) => Invoke(MoneyOrderIssued, e);

    public void Publish(TransactionVoidedEvent e) => Invoke(TransactionVoided, e);

    // Each subscriber runs on its own: one failing subscriber must not break the sale or the others.
    private void Invoke<T>(EventHandler<T>? handler, T args)
    {
        if (handler is null)
            return;

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<T>>())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A subscriber failed while handling {EventType}.", typeof(T).Name);
            }
        }
    }
}
