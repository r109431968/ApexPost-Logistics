using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Parcels;
using ApexPost.Application.Services;
using ApexPost.Domain.Parcels;
using Moq;

namespace ApexPost.UnitTests.Parcels;

/// <summary>
/// ParcelService tested with Moq. The repository, the unit of work and the clock are mocks, so these tests check
/// WHAT THE SERVICE DECIDES and WHAT IT ASKS ITS DEPENDENCIES TO DO (which calls, with which arguments, how often).
///
/// What these mocks do NOT prove: that the real repository stores scans correctly, that the scan id really is a unique
/// key in SQL Server, or that EF Core translates the queries. That is checked by the integration tests
/// (SqlPersistenceTests), which use a real database. The older ParcelServiceTests use a hand-written fake repository
/// and stay as they are: they check results, these check interactions.
///
/// The repository mock is Strict: a call that was not set up fails the test, so an unexpected extra call is caught.
/// </summary>
public class ParcelServiceMockTests
{
    private const string Tracking = "APX-12345678";
    private const string Courier = "courier-007";

    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IParcelRepository> _parcels = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IClock> _clock = new();
    private readonly ParcelService _service;

    public ParcelServiceMockTests()
    {
        _clock.SetupGet(clock => clock.UtcNow).Returns(Now);
        _unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _service = new ParcelService(_parcels.Object, _unitOfWork.Object, _clock.Object);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    private static ParcelSummary Summary(ParcelStatus status = ParcelStatus.Created) =>
        new(Tracking, 2.5m, "Asha Verma", "+44 20 7946 0000", "14 Harbour Lane", "Brightport", status, Now, Now, null);

    private static ScanInput Scan(string id, ParcelScanType type = ParcelScanType.InTransit, int minutesAgo = 60) =>
        new($"scan::{id}", type, "Brightport hub", Now.AddMinutes(-minutesAgo), null, null);

    private void ParcelExists(ParcelStatus status = ParcelStatus.Created) =>
        _parcels.Setup(repository => repository.FindAsync(Tracking, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Summary(status));

    private void AlreadyStored(params string[] scanIds) =>
        _parcels.Setup(repository => repository.ExistingScanIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(scanIds));

    private void StagingWorks()
    {
        _parcels.Setup(repository => repository.AddScansAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ScanInput>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _parcels.Setup(repository => repository.SetStatusAsync(
                It.IsAny<string>(), It.IsAny<ParcelStatus>(), It.IsAny<ParcelStatus>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ---- UploadScansAsync --------------------------------------------------------------------------------------

    [Fact]
    public async Task UploadScans_NewScans_StagesThemOnceAndCommitsOnce()
    {
        // Arrange
        ParcelExists();
        AlreadyStored();
        StagingWorks();
        var scans = new[] { Scan("a"), Scan("b") };

        // Act
        var result = await _service.UploadScansAsync(Tracking, Courier, scans, default);

        // Assert
        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.DuplicatesSkipped);
        Assert.Empty(result.Rejected);

        _parcels.Verify(repository => repository.AddScansAsync(
            Tracking, Courier, It.Is<IReadOnlyList<ScanInput>>(stored => stored.Count == 2), Now, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScans_ScanMovesParcelForward_ChangesStatusWithTheOldStatusAsExpected()
    {
        // Arrange: a Created parcel gets an InTransit scan.
        ParcelExists(ParcelStatus.Created);
        AlreadyStored();
        StagingWorks();

        // Act
        await _service.UploadScansAsync(Tracking, Courier, [Scan("a", ParcelScanType.InTransit)], default);

        // Assert: the old status is passed as "expected", which is what protects against a concurrent change.
        _parcels.Verify(repository => repository.SetStatusAsync(
            Tracking, ParcelStatus.Created, ParcelStatus.InTransit, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScans_AllScansAlreadyStored_StagesNothingAndDoesNotCommit()
    {
        // Arrange: the same batch is sent again after a dropped connection.
        ParcelExists();
        AlreadyStored("scan::a", "scan::b");

        // Act
        var result = await _service.UploadScansAsync(Tracking, Courier, [Scan("a"), Scan("b")], default);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(2, result.DuplicatesSkipped);

        _parcels.Verify(repository => repository.AddScansAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ScanInput>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScans_SameScanIdTwiceInOneBatch_CountsTheSecondOneAsDuplicate()
    {
        // Arrange
        ParcelExists();
        AlreadyStored();
        StagingWorks();

        // Act
        var result = await _service.UploadScansAsync(Tracking, Courier, [Scan("a"), Scan("a")], default);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);

        _parcels.Verify(repository => repository.AddScansAsync(
            Tracking, Courier, It.Is<IReadOnlyList<ScanInput>>(stored => stored.Count == 1), Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadScans_ScanTimeFarInTheFuture_IsRejectedAndNothingIsCommitted()
    {
        // Arrange: 10 minutes ahead is more than the 5 minute clock skew that is allowed.
        ParcelExists();
        AlreadyStored();

        // Act
        var result = await _service.UploadScansAsync(Tracking, Courier, [Scan("late", minutesAgo: -10)], default);

        // Assert
        Assert.Equal(0, result.Accepted);
        var rejection = Assert.Single(result.Rejected);
        Assert.Equal("scan::late", rejection.ScanId);
        Assert.Contains("future", rejection.Reason);

        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScans_ParcelAlreadyDelivered_RejectsTheScan()
    {
        // Arrange
        ParcelExists(ParcelStatus.Delivered);
        AlreadyStored();

        // Act
        var result = await _service.UploadScansAsync(Tracking, Courier, [Scan("a")], default);

        // Assert
        Assert.Equal(0, result.Accepted);
        var rejection = Assert.Single(result.Rejected);
        Assert.Contains("Delivered", rejection.Reason);
    }

    [Fact]
    public async Task UploadScans_UnknownShipment_ThrowsNotFoundAndLooksNoFurther()
    {
        // Arrange
        _parcels.Setup(repository => repository.FindAsync(Tracking, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParcelSummary?)null);

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UploadScansAsync(Tracking, Courier, [Scan("a")], default));

        _parcels.Verify(repository => repository.ExistingScanIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- CreateAsync and ChangeStatusAsync ---------------------------------------------------------------------

    [Fact]
    public async Task Create_DuplicateTrackingNumber_ThrowsConflictAndStagesNothing()
    {
        // Arrange
        _parcels.Setup(repository => repository.ExistsAsync(Tracking, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var parcel = new NewParcel(Tracking, 2.5m, "Asha Verma", "+44 20 7946 0000", "14 Harbour Lane", "Brightport");

        // Act + Assert
        await Assert.ThrowsAsync<ParcelConflictException>(() => _service.CreateAsync(parcel, default));

        _parcels.Verify(repository => repository.AddAsync(
            It.IsAny<NewParcel>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatus_AllowedMove_PassesTheCurrentStatusAsExpectedAndCommitsOnce()
    {
        // Arrange
        ParcelExists(ParcelStatus.Created);
        StagingWorks();

        // Act
        await _service.ChangeStatusAsync(Tracking, ParcelStatus.PickedUp, default);

        // Assert
        _parcels.Verify(repository => repository.SetStatusAsync(
            Tracking, ParcelStatus.Created, ParcelStatus.PickedUp, Now, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeStatus_MoveThatIsNotAllowed_ThrowsConflictAndChangesNothing()
    {
        // Arrange
        ParcelExists(ParcelStatus.Created);

        // Act + Assert
        await Assert.ThrowsAsync<ParcelConflictException>(() => _service.ChangeStatusAsync(Tracking, ParcelStatus.Delivered, default));

        _parcels.Verify(repository => repository.SetStatusAsync(
            It.IsAny<string>(), It.IsAny<ParcelStatus>(), It.IsAny<ParcelStatus>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}