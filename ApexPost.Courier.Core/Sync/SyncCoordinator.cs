using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Offline.Sync;

namespace ApexPost.Courier.Core.Sync;

/// <summary>
/// Decides WHEN to sync and remembers the last result for the Sync Status screen. It does not decide how: that is
/// <see cref="BatchSyncService"/>. A run is started by the app when the network comes back, when a scan is saved, when the courier
/// presses Sync now, and by a timer. Nothing is sent when nobody is signed in.
/// </summary>
public sealed class SyncCoordinator(BatchSyncService sync, AuthService auth)
{
    public SyncRunResult? LastResult { get; private set; }

    public DateTimeOffset? LastRunAtUtc { get; private set; }

    /// <summary>Raised after a run finished. Can be raised on a background thread.</summary>
    public event EventHandler? Changed;

    public async Task<SyncRunResult?> TriggerAsync(CancellationToken cancellationToken = default)
    {
        if (!auth.IsSignedIn)
        {
            return null;
        }

        var result = await sync.SyncAsync(cancellationToken).ConfigureAwait(false);
        if (result.StopReason != SyncStopReason.AlreadyRunning && (result.Attempted > 0 || LastResult is null))
        {
            LastResult = result;
            LastRunAtUtc = DateTimeOffset.UtcNow;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    /// <summary>Tries a sync every <paramref name="interval"/> until cancelled. This is how the app finds out that a down server is back.</summary>
    public async Task RunPeriodicallyAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await TriggerAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
    }
}

