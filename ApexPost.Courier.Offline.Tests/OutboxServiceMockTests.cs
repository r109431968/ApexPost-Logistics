using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;
using Moq;
using System.Timers;

namespace ApexPost.Courier.Offline.Tests;

/// <summary>
/// OutboxService tested with Moq. The local store (IScanRepository) and the central system (IShipmentUploader)
/// are both mocks, so no Couchbase database and no network are involved.
///
/// What these mocks do NOT prove: that Couchbase really stores the new syncStatus, or that the real uploader sends the
/// right HTTP request. The Update call is only verified as "the outbox asked for it". The real store is covered by
/// ScanRepositoryTests and OfflineCarryOverTests, the real uploader by the Courier.Core tests.
/// The older OutboxServiceTests use the real embedded store; they stay as they are.
/// </summary>
public class OutboxServiceMockTests
{
    private readonly Mock<IScanRepository> _repository = new();
    private readonly Mock<IShipmentUploader> _uploader = new();
    private readonly OutboxService _outbox;

    public OutboxServiceMockTests()
    {
        // By default the store finds the scan it is asked to update.
        _repository.Setup(repository => repository.Update(It.IsAny<ShipmentScan>())).Returns(true);
        _outbox = new OutboxService(_repository.Object, _uploader.Object);
    }

    private static ShipmentScan NewScan(string tracking) =>
        ShipmentScan.CreatePending(tracking, ScanType.Pickup, "COURIER-001", "Demo Depot");

    private void PendingScans(params ShipmentScan[] scans) =>
        _repository.Setup(repository => repository.ListPending()).Returns(scans);

    private void UploaderAnswers(bool accepted) =>
        _uploader.Setup(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accepted);

    [Fact]
    public async Task ProcessPending_ScanAccepted_UpdatesItAsSynced()
    {
        // Arrange
        var scan = NewScan("APX-DEMO-0001");
        PendingScans(scan);
        UploaderAnswers(true);

        // Act
        var result = await _outbox.ProcessPendingAsync();

        // Assert
        Assert.Equal(new OutboxResult(1, 1, 0), result);
        _repository.Verify(repository => repository.Update(
            It.Is<ShipmentScan>(updated => updated.Id == scan.Id && updated.SyncStatus == SyncStatus.Synced)), Times.Once);
    }

    [Fact]
    public async Task ProcessPending_ScanRejected_UpdatesItAsFailed()
    {
        // Arrange
        var scan = NewScan("FAIL-DEMO-0001");
        PendingScans(scan);
        UploaderAnswers(false);

        // Act
        var result = await _outbox.ProcessPendingAsync();

        // Assert
        Assert.Equal(new OutboxResult(1, 0, 1), result);
        _repository.Verify(repository => repository.Update(
            It.Is<ShipmentScan>(updated => updated.Id == scan.Id && updated.SyncStatus == SyncStatus.Failed)), Times.Once);
    }

    [Fact]
    public async Task ProcessPending_NothingPending_NeverCallsTheUploader()
    {
        // Arrange
        PendingScans();

        // Act
        var result = await _outbox.ProcessPendingAsync();

        // Assert
        Assert.Equal(new OutboxResult(0, 0, 0), result);
        _uploader.Verify(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPending_ServerUnavailable_StopsTheRunAndLeavesEveryScanPending()
    {
        // Arrange: the very first upload finds no network.
        PendingScans(NewScan("APX-DEMO-0001"), NewScan("APX-DEMO-0002"));
        _uploader.Setup(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UploadUnavailableException("no network"));

        // Act
        var result = await _outbox.ProcessPendingAsync();

        // Assert: it did not even try the second scan, and nothing was marked Synced or Failed.
        Assert.Equal(new OutboxResult(2, 0, 0), result);
        _uploader.Verify(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(repository => repository.Update(It.IsAny<ShipmentScan>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPending_OneUploadThrows_MarksThatScanFailedAndContinuesWithTheNext()
    {
        // Arrange: the general answer is "accepted"; the more specific setup for BOOM wins because it is set up later.
        var broken = NewScan("BOOM-DEMO-0001");
        var fine = NewScan("APX-DEMO-0002");
        PendingScans(broken, fine);
        UploaderAnswers(true);
        _uploader.Setup(uploader => uploader.UploadAsync(
                It.Is<ShipmentScan>(scan => scan.TrackingNumber == "BOOM-DEMO-0001"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bad scan"));

        // Act
        var result = await _outbox.ProcessPendingAsync();

        // Assert
        Assert.Equal(new OutboxResult(2, 1, 1), result);
        _repository.Verify(repository => repository.Update(
            It.Is<ShipmentScan>(updated => updated.Id == broken.Id && updated.SyncStatus == SyncStatus.Failed)), Times.Once);
        _repository.Verify(repository => repository.Update(
            It.Is<ShipmentScan>(updated => updated.Id == fine.Id && updated.SyncStatus == SyncStatus.Synced)), Times.Once);
    }

    [Fact]
    public async Task ProcessPending_ScanDeletedDuringUpload_IsNotCountedAsSyncedOrFailed()
    {
        // Arrange: Update returns false, which means "no such scan any more".
        PendingScans(NewScan("APX-DEMO-0001"));
        UploaderAnswers(true);
        _repository.Setup(repository => repository.Update(It.IsAny<ShipmentScan>())).Returns(false);

        // Act
        var result = await _outbox.ProcessPendingAsync();

        // Assert
        Assert.Equal(new OutboxResult(1, 0, 0), result);
    }

    [Fact]
    public async Task ProcessPending_AlreadyCancelled_ThrowsAndNeverCallsTheUploader()
    {
        // Arrange
        PendingScans(NewScan("APX-DEMO-0001"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _outbox.ProcessPendingAsync(cancelled.Token));

        _uploader.Verify(uploader => uploader.UploadAsync(It.IsAny<ShipmentScan>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(repository => repository.Update(It.IsAny<ShipmentScan>()), Times.Never);
    }
}