using ApexPost.Courier.Offline.Abstractions;

namespace ApexPost.Courier.Offline.Sync;

public enum SyncStopReason
{
    /// <summary>Every pending scan was handled.</summary>
    Finished,

    /// <summary>The network or the server was not reachable. The rest stays Pending.</summary>
    Offline,

    /// <summary>The access token was refused. The rest stays Pending until the courier signs in again.</summary>
    SessionExpired,

    /// <summary>Another run was already going on, so this call did nothing.</summary>
    AlreadyRunning
}

/// <param name="Attempted">Scans that were sent.</param>
/// <param name="Synced">Scans the central system has now.</param>
/// <param name="Rejected">Scans the central system refused (now Failed).</param>
/// <param name="StillPending">Scans that are Pending after the run.</param>
/// <param name="Batches">Number of batches that were sent.</param>
/// <param name="StopReason">Why the run ended.</param>
/// <param name="Rejections">The refused scans, with the reasons.</param>
public sealed record SyncRunResult(
    int Attempted,
    int Synced,
    int Rejected,
    int StillPending,
    int Batches,
    SyncStopReason StopReason,
    IReadOnlyList<ScanOutcome> Rejections)
{
    public static SyncRunResult AlreadyRunning(int pending) =>
        new(0, 0, 0, pending, 0, SyncStopReason.AlreadyRunning, []);
}

/// <summary>
/// Part D outbox: sends the Pending scans oldest first in batches (20 by default) and updates syncStatus from the answer for each scan.
/// - Synced scans become Synced, refused scans become Failed.
/// - If the network or the server is not reachable, or the token is refused, the run stops and those scans stay Pending.
///   Offline is a normal state for a courier, not a failure of the scan.
/// - Only one run at a time: a second call while a run is going on returns <see cref="SyncStopReason.AlreadyRunning"/>.
/// </summary>
public sealed class BatchSyncService
{
    public const int DefaultBatchSize = 20;

    private readonly IScanRepository _repository;
    private readonly IScanBatchUploader _uploader;
    private readonly int _batchSize;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BatchSyncService(IScanRepository repository, IScanBatchUploader uploader, int batchSize = DefaultBatchSize)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(uploader);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        _repository = repository;
        _uploader = uploader;
        _batchSize = batchSize;
    }

    /// <summary>Raised after every run (not for AlreadyRunning). Can be raised on a background thread.</summary>
    public event EventHandler<SyncRunResult>? RunCompleted;

    public bool IsRunning => _gate.CurrentCount == 0;

    public async Task<SyncRunResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return SyncRunResult.AlreadyRunning(_repository.CountPending());
        }

        SyncRunResult result;
        try
        {
            result = await RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        RunCompleted?.Invoke(this, result);
        return result;
    }

    private async Task<SyncRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var pending = _repository.ListPending();   // oldest first
        var attempted = 0;
        var synced = 0;
        var batches = 0;
        var rejections = new List<ScanOutcome>();
        var stop = SyncStopReason.Finished;

        foreach (var batch in pending.Chunk(_batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            batches++;
            attempted += batch.Length;

            IReadOnlyList<ScanOutcome> outcomes;
            try
            {
                outcomes = await _uploader.UploadBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The uploader should not throw. If it does, nothing is known about these scans, so they stay Pending.
                outcomes = [.. batch.Select(scan => new ScanOutcome(scan.Id, ScanOutcomeKind.RetryLater, ex.Message))];
            }

            var byId = outcomes.GroupBy(outcome => outcome.ScanId).ToDictionary(group => group.Key, group => group.First());
            var sawRetry = false;
            var sawUnauthorized = false;

            foreach (var scan in batch)
            {
                var outcome = byId.GetValueOrDefault(scan.Id) ?? new ScanOutcome(scan.Id, ScanOutcomeKind.RetryLater, "No answer for this scan.");
                switch (outcome.Kind)
                {
                    case ScanOutcomeKind.Synced:
                        if (_repository.Update(scan.MarkSynced()))
                        {
                            synced++;
                        }

                        break;
                    case ScanOutcomeKind.Rejected:
                        if (_repository.Update(scan.MarkFailed()))
                        {
                            rejections.Add(outcome);
                        }

                        break;
                    case ScanOutcomeKind.Unauthorized:
                        sawUnauthorized = true;
                        break;
                    default:
                        sawRetry = true;
                        break;
                }
            }

            if (sawUnauthorized)
            {
                stop = SyncStopReason.SessionExpired;
                break;
            }

            if (sawRetry)
            {
                stop = SyncStopReason.Offline;
                break;
            }
        }

        return new SyncRunResult(attempted, synced, rejections.Count, _repository.CountPending(), batches, stop, rejections);
    }
}
