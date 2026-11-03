using ApexPost.Courier.Offline.Abstractions;

namespace ApexPost.Courier.Offline.Sync;

/// <summary>Result of one outbox run.</summary>
public sealed record OutboxResult(int Attempted, int Synced, int Failed);

/// <summary>
/// The outbox: it reads the Pending scans from the local store, hands each one to the uploader,
/// and marks it Synced (accepted) or Failed (rejected, or the upload threw). Only this class changes syncStatus.
/// </summary>
public sealed class OutboxService
{
    private readonly IScanRepository _repository;
    private readonly IShipmentUploader _uploader;

    public OutboxService(IScanRepository repository, IShipmentUploader uploader)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(uploader);
        _repository = repository;
        _uploader = uploader;
    }

    public async Task<OutboxResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = _repository.ListPending();
        var synced = 0;
        var failed = 0;

        foreach (var scan in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool accepted;
            try
            {
                accepted = await _uploader.UploadAsync(scan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                accepted = false;   // one bad upload must not stop the other scans
            }

            var updated = accepted ? scan.MarkSynced() : scan.MarkFailed();
            if (!_repository.Update(updated))
            {
                continue;   // the scan was deleted while the upload was running
            }

            if (accepted)
            {
                synced++;
            }
            else
            {
                failed++;
            }
        }

        return new OutboxResult(pending.Count, synced, failed);
    }
}
