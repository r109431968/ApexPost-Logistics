using System.Net;
using System.Text.Json;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;

namespace ApexPost.Courier.Core.Tests;

public class BatchUploaderTests
{
    private static ShipmentScan Scan(string tracking, ScanType type = ScanType.InTransit, string courier = "courier-007", string? signature = null) =>
        ShipmentScan.CreatePending(tracking, type, courier, "Brightport hub", recipientSignatureName: signature);

    [Fact]
    public async Task Scans_of_different_shipments_go_in_separate_requests_and_all_become_synced()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK, Http.UploadOk)));
        using var harness = new Harness(network);
        var uploader = new ApiScanBatchUploader(harness.Client);
        var a1 = Scan("APX-11111111");
        var a2 = Scan("APX-11111111");
        var b1 = Scan("APX-22222222");

        var outcomes = await uploader.UploadBatchAsync([a1, a2, b1]);

        Assert.Equal(2, network.Calls);
        Assert.All(outcomes, outcome => Assert.Equal(ScanOutcomeKind.Synced, outcome.Kind));
        Assert.Equal(new[] { a1.Id, a2.Id, b1.Id }, outcomes.Select(outcome => outcome.ScanId).ToArray());
    }

    [Fact]
    public async Task The_wire_format_has_the_text_scan_type_and_the_signature_name()
    {
        string? body = null;
        var network = new StubHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Http.Json(HttpStatusCode.OK, Http.UploadOk);
        });
        using var harness = new Harness(network);
        var scan = Scan("APX-11111111", ScanType.Delivered, signature: "Asha Verma");

        await new ApiScanBatchUploader(harness.Client).UploadBatchAsync([scan]);

        using var json = JsonDocument.Parse(body!);
        Assert.Equal("courier-007", json.RootElement.GetProperty("courierId").GetString());
        var item = json.RootElement.GetProperty("scans")[0];
        Assert.Equal(scan.Id, item.GetProperty("scanId").GetString());
        Assert.Equal("Delivered", item.GetProperty("scanType").GetString());
        Assert.Equal("Asha Verma", item.GetProperty("recipientSignatureName").GetString());
        Assert.False(item.TryGetProperty("notes", out _));   // null fields are left out
    }

    [Fact]
    public async Task A_scan_the_server_rejected_is_rejected_and_the_others_are_synced()
    {
        var bad = Scan("APX-11111111");
        var good = Scan("APX-11111111");
        var network = new StubHandler((_, _) => Task.FromResult(Http.Json(HttpStatusCode.OK,
            $$"""{"accepted":1,"duplicatesSkipped":0,"rejected":[{"scanId":"{{bad.Id}}","reason":"Pickup is not possible after Delivered."}]}""")));
        using var harness = new Harness(network);

        var outcomes = await new ApiScanBatchUploader(harness.Client).UploadBatchAsync([bad, good]);

        Assert.Equal(ScanOutcomeKind.Rejected, outcomes.Single(outcome => outcome.ScanId == bad.Id).Kind);
        Assert.Contains("Delivered", outcomes.Single(outcome => outcome.ScanId == bad.Id).Reason);
        Assert.Equal(ScanOutcomeKind.Synced, outcomes.Single(outcome => outcome.ScanId == good.Id).Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_decision_by_the_server_rejects_the_scans_of_that_shipment_only(HttpStatusCode status)
    {
        var network = new StubHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.Contains("APX-11111111")
            ? Http.Problem(status, "Refused")
            : Http.Json(HttpStatusCode.OK, Http.UploadOk)));
        using var harness = new Harness(network, token: "a.b.c");
        var first = Scan("APX-11111111");
        var second = Scan("APX-22222222");

        var outcomes = await new ApiScanBatchUploader(harness.Client).UploadBatchAsync([first, second]);

        Assert.Equal(ScanOutcomeKind.Rejected, outcomes.Single(outcome => outcome.ScanId == first.Id).Kind);
        Assert.Equal(ScanOutcomeKind.Synced, outcomes.Single(outcome => outcome.ScanId == second.Id).Kind);
    }

    [Fact]
    public async Task When_the_network_is_down_every_scan_stays_pending_and_the_rest_is_not_tried()
    {
        var network = new StubHandler((_, _) => throw new HttpRequestException("down"));
        using var harness = new Harness(network);
        var scans = new[] { Scan("APX-11111111"), Scan("APX-22222222"), Scan("APX-33333333") };

        var outcomes = await new ApiScanBatchUploader(harness.Client).UploadBatchAsync(scans);

        Assert.Equal(3, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.Equal(ScanOutcomeKind.RetryLater, outcome.Kind));
        Assert.Equal(4, network.Calls);   // only the first shipment was tried (1 try + 3 retries)
    }

    [Fact]
    public async Task A_401_stops_the_batch_with_unauthorized()
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(HttpStatusCode.Unauthorized, "Unauthorized")));
        using var harness = new Harness(network, token: "expired.token.value");
        var scans = new[] { Scan("APX-11111111"), Scan("APX-22222222") };

        var outcomes = await new ApiScanBatchUploader(harness.Client).UploadBatchAsync(scans);

        Assert.All(outcomes, outcome => Assert.Equal(ScanOutcomeKind.Unauthorized, outcome.Kind));
        Assert.Equal(1, network.Calls);
    }
}
