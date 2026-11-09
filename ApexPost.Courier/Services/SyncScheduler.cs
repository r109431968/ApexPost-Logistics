using Microsoft.Extensions.Logging;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Sync;

namespace ApexPost.Courier.Services;

/// <summary>
/// Starts the sync at the right moments: when the network comes back (Connectivity.ConnectivityChanged), after sign-in,
/// every 30 seconds (this also finds out that a server which was down is back), and when a screen asks for it.
/// The sync itself is in BatchSyncService; this class only decides when.
/// </summary>
public sealed class SyncScheduler(SyncCoordinator coordinator, AuthService auth, ILogger<SyncScheduler> logger) : IDisposable
{
    private CancellationTokenSource? _cts;

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        auth.Changed += OnAuthChanged;
        _ = coordinator.RunPeriodicallyAsync(AppSettings.PeriodicSyncInterval, _cts.Token);
    }

    public Task RequestSyncAsync() => SafeTriggerAsync("requested");

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        logger.LogInformation("Connectivity changed: {Access}.", e.NetworkAccess);
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _ = SafeTriggerAsync("network is back");
        }
    }

    private void OnAuthChanged(object? sender, EventArgs e)
    {
        if (auth.IsSignedIn)
        {
            _ = SafeTriggerAsync("signed in");
        }
    }

    private async Task SafeTriggerAsync(string reason)
    {
        try
        {
            logger.LogInformation("Sync started: {Reason}.", reason);
            await coordinator.TriggerAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sync failed unexpectedly.");
        }
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        auth.Changed -= OnAuthChanged;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
