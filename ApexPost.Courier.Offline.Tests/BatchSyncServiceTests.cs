using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;

namespace ApexPost.Courier.Offline.Tests;

public class BatchSyncServiceTests
{
    /// <summary>The store in memory: these tests are about the sync rules, not about Couchbase.</summary>
    private sealed class MemoryRepository : IScanRepository
    {
        private readonly Dictionary<string, ShipmentScan> _scans = [];

        public ShipmentScan Create(ShipmentScan scan) => _scans[scan.Id] = scan with { SyncStatus = SyncStatus.Pending };
        public ShipmentScan? GetById(string id) => _scans.GetValueOrDefault(id);
        public bool Update(ShipmentScan scan) => _scans.ContainsKey(scan.Id) && (_scans[scan.Id] = scan) is not null;
        public bool Delete(string id) => _scans.Remove(id);
        public IReadOnlyList<ShipmentScan> ListAll() => [.. _scans.Values.OrderBy(scan => scan.ScannedAtUtc)];
        public IReadOnlyList<ShipmentScan> FindByTrackingNumber(string trackingNumber) => [.. ListAll().Where(scan => scan.TrackingNumber == trackingNumber)];
        public IReadOnlyList<ShipmentScan> ListPending() => [.. ListAll().Where(scan => scan.SyncStatus == SyncStatus.Pending)];
        public int CountPending() => ListPending().Count;
        public IReadOnlyDictionary<ScanType, int> CountByScanType(string courierId) =>
            _scans.Values.Where(scan => scan.CourierId == courierId).GroupBy(scan => scan.ScanType).ToDictionary(group => group.Key, group => group.Count());
    }

    private sealed class ScriptedUploader(Func<IReadOnlyList<ShipmentScan>, int, IReadOnlyList<ScanOutcome>> answer) : IScanBatchUploader
    {
        public List<int> BatchSizes { get; } = [];

        public Task<IReadOnlyList<ScanOutcome>> UploadBatchAsync(IReadOnlyList<ShipmentScan> batch, CancellationToken cancellationToken = default)
        {
            BatchSizes.Add(batch.Count);
            return Task.FromResult(answer(batch, BatchSizes.Count));
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    private static MemoryRepository RepositoryWith(int count, string tracking = "APX-11111111")
    {
        var repository = new MemoryRepository();
        for (var i = 0; i < count; i++)
        {
            repository.Create(ShipmentScan.CreatePending(tracking, ScanType.InTransit, "courier-007", "Hub", scannedAtUtc: Start.AddMinutes(i)));
        }

        return repository;
    }

    private static IReadOnlyList<ScanOutcome> All(IReadOnlyList<ShipmentScan> batch, ScanOutcomeKind kind, string? reason = null) =>
        [.. batch.Select(scan => new ScanOutcome(scan.Id, kind, reason))];

    [Fact]
    public async Task Fifty_scans_are_sent_in_batches_of_twenty()
    {
        var repository = RepositoryWith(50);
        var uploader = new ScriptedUploader((batch, _) => All(batch, ScanOutcomeKind.Synced));

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal([20, 20, 10], uploader.BatchSizes);
        Assert.Equal(3, result.Batches);
        Assert.Equal(50, result.Attempted);
        Assert.Equal(50, result.Synced);
        Assert.Equal(0, result.StillPending);
        Assert.Equal(SyncStopReason.Finished, result.StopReason);
    }

    [Fact]
    public async Task The_oldest_scans_go_first()
    {
        var repository = RepositoryWith(3);
        IReadOnlyList<ShipmentScan>? first = null;
        var uploader = new ScriptedUploader((batch, _) => { first = batch; return All(batch, ScanOutcomeKind.Synced); });

        await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal(first!.OrderBy(scan => scan.ScannedAtUtc).Select(scan => scan.Id), first!.Select(scan => scan.Id));
    }

    [Fact]
    public async Task A_rejected_scan_becomes_failed_and_the_others_synced_and_the_reason_is_reported()
    {
        var repository = RepositoryWith(3);
        var bad = repository.ListPending()[1];
        var uploader = new ScriptedUploader((batch, _) =>
            [.. batch.Select(scan => scan.Id == bad.Id
                ? new ScanOutcome(scan.Id, ScanOutcomeKind.Rejected, "Not possible.")
                : new ScanOutcome(scan.Id, ScanOutcomeKind.Synced))]);

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal(SyncStatus.Failed, repository.GetById(bad.Id)!.SyncStatus);
        Assert.Equal(2, result.Synced);
        Assert.Equal(1, result.Rejected);
        Assert.Equal("Not possible.", result.Rejections.Single().Reason);
        Assert.Equal(0, result.StillPending);
    }

    [Fact]
    public async Task When_the_network_is_down_the_scans_stay_pending_and_the_run_stops_after_the_first_batch()
    {
        var repository = RepositoryWith(45);
        var uploader = new ScriptedUploader((batch, _) => All(batch, ScanOutcomeKind.RetryLater, "offline"));

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal([20], uploader.BatchSizes);                 // no point in sending batch 2 and 3
        Assert.Equal(45, result.StillPending);
        Assert.Equal(0, repository.ListAll().Count(scan => scan.SyncStatus == SyncStatus.Failed));   // offline is not a failure of the scan
        Assert.Equal(SyncStopReason.Offline, result.StopReason);
    }

    [Fact]
    public async Task The_network_going_away_in_the_middle_keeps_what_was_sent_and_leaves_the_rest_pending()
    {
        var repository = RepositoryWith(30);
        var uploader = new ScriptedUploader((batch, number) => All(batch, number == 1 ? ScanOutcomeKind.Synced : ScanOutcomeKind.RetryLater));

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal(20, result.Synced);
        Assert.Equal(10, result.StillPending);
        Assert.Equal(SyncStopReason.Offline, result.StopReason);
    }

    [Fact]
    public async Task An_expired_session_stops_the_run_and_nothing_is_marked()
    {
        var repository = RepositoryWith(25);
        var uploader = new ScriptedUploader((batch, _) => All(batch, ScanOutcomeKind.Unauthorized));

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal(SyncStopReason.SessionExpired, result.StopReason);
        Assert.Equal(25, result.StillPending);
        Assert.Equal([20], uploader.BatchSizes);
    }

    [Fact]
    public async Task An_uploader_that_throws_leaves_the_scans_pending()
    {
        var repository = RepositoryWith(5);
        var uploader = new ScriptedUploader((_, _) => throw new InvalidOperationException("boom"));

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal(5, result.StillPending);
        Assert.Equal(SyncStopReason.Offline, result.StopReason);
    }

    [Fact]
    public async Task A_scan_without_an_answer_stays_pending()
    {
        var repository = RepositoryWith(2);
        var uploader = new ScriptedUploader((batch, _) => [new ScanOutcome(batch[0].Id, ScanOutcomeKind.Synced)]);

        var result = await new BatchSyncService(repository, uploader).SyncAsync();

        Assert.Equal(1, result.Synced);
        Assert.Equal(1, result.StillPending);
    }

    [Fact]
    public async Task A_second_call_during_a_run_does_nothing()
    {
        var repository = RepositoryWith(2);
        var release = new TaskCompletionSource();
        var uploader = new SlowUploader(release.Task);
        var service = new BatchSyncService(repository, uploader);

        var running = service.SyncAsync();
        await uploader.Started.Task;
        var second = await service.SyncAsync();
        release.SetResult();
        var first = await running;

        Assert.Equal(SyncStopReason.AlreadyRunning, second.StopReason);
        Assert.Equal(0, second.Attempted);
        Assert.Equal(2, first.Synced);
        Assert.Equal(1, uploader.Calls);
    }

    [Fact]
    public async Task RunCompleted_is_raised_once_per_run()
    {
        var repository = RepositoryWith(1);
        var service = new BatchSyncService(repository, new ScriptedUploader((batch, _) => All(batch, ScanOutcomeKind.Synced)));
        var raised = new List<SyncRunResult>();
        service.RunCompleted += (_, result) => raised.Add(result);

        await service.SyncAsync();

        Assert.Single(raised);
        Assert.Equal(1, raised[0].Synced);
    }

    private sealed class SlowUploader(Task release) : IScanBatchUploader
    {
        public TaskCompletionSource Started { get; } = new();
        public int Calls { get; private set; }

        public async Task<IReadOnlyList<ScanOutcome>> UploadBatchAsync(IReadOnlyList<ShipmentScan> batch, CancellationToken cancellationToken = default)
        {
            Calls++;
            Started.SetResult();
            await release;
            return [.. batch.Select(scan => new ScanOutcome(scan.Id, ScanOutcomeKind.Synced))];
        }
    }
}
