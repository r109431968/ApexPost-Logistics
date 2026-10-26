using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Settings;
using ApexPost.Domain.Events;

namespace ApexPost.Application.Services;

/// <summary>Simulated notifications: low stock, high-value money orders, and every void or refund.</summary>
public sealed class NotificationSubscriber(
    CounterEvents events,
    INotificationSink sink,
    MoneyOrderSettings moneyOrderSettings)
{
    private bool _attached;

    public void Attach()
    {
        if (_attached)
            return;

        _attached = true;
        events.StockLow += OnStockLow;
        events.MoneyOrderIssued += OnMoneyOrderIssued;
        events.TransactionVoided += OnTransactionVoided;
    }

    private void OnStockLow(object? sender, StockLowEvent e) =>
        sink.Send(new Notification(
            NotificationKind.LowStock,
            $"Low stock at {e.BranchCode}",
            $"{e.Sku} has {e.Remaining} left (threshold {e.Threshold}).",
            e.OccurredAt));

    private void OnMoneyOrderIssued(object? sender, MoneyOrderIssuedEvent e)
    {
        if (e.Amount.Amount < moneyOrderSettings.HighValueAlertThreshold)
            return;

        sink.Send(new Notification(
            NotificationKind.HighValueMoneyOrder,
            $"High-value money order at {e.BranchCode}",
            $"Amount {e.Amount}, identity verified: {e.IdentityVerified}.",
            e.OccurredAt));
    }

    private void OnTransactionVoided(object? sender, TransactionVoidedEvent e) =>
        sink.Send(new Notification(
            NotificationKind.VoidOrRefund,
            e.IsRefund ? $"Refund at {e.BranchCode}" : $"Void at {e.BranchCode}",
            $"Transaction {e.TransactionId}, reason: {e.Reason}.",
            e.OccurredAt));
}
