using ApexPost.Application.Exceptions;
using ApexPost.Application.Parcels;
using ApexPost.Application.Services;
using ApexPost.Domain.Parcels;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Parcels;

public class ParcelServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeParcelRepository _repository = new();
    private readonly SpyUnitOfWork _unitOfWork = new();
    private readonly ParcelService _service;

    public ParcelServiceTests() => _service = new ParcelService(_repository, _unitOfWork, new FixedClock(Now));

    private static NewParcel Parcel(string tracking = "APX-12345678") =>
        new(tracking, 2.5m, "Asha Verma", "+44 20 7946 0000", "14 Harbour Lane", "Brightport");

    private static ScanInput Scan(string id, ParcelScanType type = ParcelScanType.InTransit, int minutesAgo = 60, string? signature = null) =>
        new($"scan::{id}", type, "Brightport hub", Now.AddMinutes(-minutesAgo), null, signature);

    // ---- Transitions (pure rules) ------------------------------------------------------------------------------

    [Theory]
    [InlineData(ParcelStatus.Created, ParcelStatus.PickedUp, true)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.InTransit, true)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.OutForDelivery, true)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.Delivered, true)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.Failed, true)]
    [InlineData(ParcelStatus.Created, ParcelStatus.InTransit, false)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.Delivered, false)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.Created, false)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.Failed, false)]
    [InlineData(ParcelStatus.Failed, ParcelStatus.PickedUp, false)]
    public void Only_the_documented_status_changes_are_allowed(ParcelStatus from, ParcelStatus to, bool allowed) =>
        Assert.Equal(allowed, ParcelTransitions.CanMove(from, to));

    // ---- Create, change, delete --------------------------------------------------------------------------------

    [Fact]
    public async Task A_new_shipment_starts_in_status_Created_and_is_committed_once()
    {
        var created = await _service.CreateAsync(Parcel(), default);

        Assert.Equal(ParcelStatus.Created, created.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task A_duplicate_tracking_number_is_a_conflict_and_commits_nothing()
    {
        await _service.CreateAsync(Parcel(), default);
        var commits = _unitOfWork.SaveCount;

        await Assert.ThrowsAsync<ParcelConflictException>(() => _service.CreateAsync(Parcel(), default));

        Assert.Equal(commits, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task An_unknown_tracking_number_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync("APX-99999999", default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ScansAsync("APX-99999999", default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync("APX-99999999", default));
    }

    [Fact]
    public async Task A_status_change_that_skips_a_step_is_a_conflict()
    {
        await _service.CreateAsync(Parcel(), default);

        var error = await Assert.ThrowsAsync<ParcelConflictException>(
            () => _service.ChangeStatusAsync("APX-12345678", ParcelStatus.Delivered, default));

        Assert.Contains("Created", error.Message);
        Assert.Contains("Delivered", error.Message);
    }

    [Fact]
    public async Task The_full_happy_path_ends_in_Delivered_and_then_nothing_can_change()
    {
        await _service.CreateAsync(Parcel(), default);

        foreach (var status in new[] { ParcelStatus.PickedUp, ParcelStatus.InTransit, ParcelStatus.OutForDelivery, ParcelStatus.Delivered })
            Assert.Equal(status, (await _service.ChangeStatusAsync("APX-12345678", status, default)).Status);

        await Assert.ThrowsAsync<ParcelConflictException>(() => _service.ChangeStatusAsync("APX-12345678", ParcelStatus.Failed, default));
        await Assert.ThrowsAsync<ParcelConflictException>(() => _service.ReplaceAsync(
            "APX-12345678", new ParcelEdits(1m, "X", "+44 20 7946 0000", "Y", "Z"), default));
    }

    [Fact]
    public async Task Only_a_shipment_in_status_Created_can_be_deleted()
    {
        await _service.CreateAsync(Parcel("APX-00000001"), default);
        await _service.CreateAsync(Parcel("APX-00000002"), default);
        await _service.ChangeStatusAsync("APX-00000002", ParcelStatus.PickedUp, default);

        await _service.DeleteAsync("APX-00000001", default);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync("APX-00000001", default));
        await Assert.ThrowsAsync<ParcelConflictException>(() => _service.DeleteAsync("APX-00000002", default));
    }

    [Fact]
    public async Task The_list_is_paged_and_filtered()
    {
        for (var i = 1; i <= 5; i++)
            await _service.CreateAsync(Parcel($"APX-0000000{i}"), default);
        await _service.ChangeStatusAsync("APX-00000001", ParcelStatus.PickedUp, default);

        var page = await _service.ListAsync(new ParcelFilter(2, 2, null, null), default);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(new[] { "APX-00000003", "APX-00000004" }, page.Items.Select(p => p.TrackingNumber).ToArray());

        var created = await _service.ListAsync(new ParcelFilter(1, 20, ParcelStatus.Created, "brightport"), default);
        Assert.Equal(4, created.TotalCount);
    }

    // ---- Idempotent scan upload --------------------------------------------------------------------------------

    [Fact]
    public async Task Uploading_the_same_scans_twice_stores_them_once()
    {
        await _service.CreateAsync(Parcel(), default);
        ScanInput[] batch = [Scan("11111111-1111-4111-8111-111111111111"), Scan("22222222-2222-4222-8222-222222222222", minutesAgo: 30)];

        var first = await _service.UploadScansAsync("APX-12345678", "courier-007", batch, default);
        var second = await _service.UploadScansAsync("APX-12345678", "courier-007", batch, default);

        Assert.Equal(2, first.Accepted);
        Assert.Equal(0, first.DuplicatesSkipped);
        Assert.Equal(0, second.Accepted);
        Assert.Equal(2, second.DuplicatesSkipped);
        Assert.Equal(2, _repository.StoredScanCount);
    }

    [Fact]
    public async Task The_same_scan_twice_inside_one_batch_counts_once()
    {
        await _service.CreateAsync(Parcel(), default);
        var scan = Scan("33333333-3333-4333-8333-333333333333");

        var result = await _service.UploadScansAsync("APX-12345678", "courier-007", [scan, scan], default);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(1, _repository.StoredScanCount);
    }

    [Fact]
    public async Task A_scan_from_the_future_is_rejected_with_a_reason_and_the_rest_are_accepted()
    {
        await _service.CreateAsync(Parcel(), default);
        var future = Scan("44444444-4444-4444-8444-444444444444", minutesAgo: -600);
        var good = Scan("55555555-5555-4555-8555-555555555555");

        var result = await _service.UploadScansAsync("APX-12345678", "courier-007", [future, good], default);

        Assert.Equal(1, result.Accepted);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(future.ScanId, rejected.ScanId);
        Assert.Contains("future", rejected.Reason);
        Assert.Equal(2, result.Accepted + result.DuplicatesSkipped + result.Rejected.Count);
    }

    [Fact]
    public async Task Scans_move_the_shipment_forward_and_a_delivered_shipment_refuses_more_scans()
    {
        await _service.CreateAsync(Parcel(), default);

        var pickup = Scan("66666666-6666-4666-8666-666666666666", ParcelScanType.Pickup, minutesAgo: 120);
        var delivered = Scan("77777777-7777-4777-8777-777777777777", ParcelScanType.Delivered, minutesAgo: 30, signature: "Asha Verma");
        await _service.UploadScansAsync("APX-12345678", "courier-007", [pickup, delivered], default);

        Assert.Equal(ParcelStatus.Delivered, (await _service.GetAsync("APX-12345678", default)).Status);

        var late = Scan("88888888-8888-4888-8888-888888888888", minutesAgo: 5);
        var result = await _service.UploadScansAsync("APX-12345678", "courier-007", [late], default);

        Assert.Equal(0, result.Accepted);
        Assert.Contains("Delivered", Assert.Single(result.Rejected).Reason);
    }

    [Fact]
    public async Task An_upload_commits_once_and_a_batch_with_nothing_new_commits_nothing()
    {
        await _service.CreateAsync(Parcel(), default);
        var scan = Scan("99999999-9999-4999-8999-999999999999");

        var before = _unitOfWork.SaveCount;
        await _service.UploadScansAsync("APX-12345678", "courier-007", [scan], default);
        Assert.Equal(before + 1, _unitOfWork.SaveCount);

        await _service.UploadScansAsync("APX-12345678", "courier-007", [scan], default);
        Assert.Equal(before + 1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Uploading_to_an_unknown_shipment_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UploadScansAsync(
            "APX-99999999", "courier-007", [Scan("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")], default));
    }
}
