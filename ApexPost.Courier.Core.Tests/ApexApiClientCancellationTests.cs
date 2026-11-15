using ApexPost.Courier.Core.Api;

namespace ApexPost.Courier.Core.Tests;

/// <summary>
/// The caller cancels (for example the app is closing). That is not a server problem: the call must stop at once,
/// must not be retried, and must not switch the "Server unavailable" banner on.
/// </summary>
public class ApexApiClientCancellationTests
{
    private static readonly IReadOnlyList<ScanItemDto> Scans =
        [new("scan::3f2504e0-4f89-41d3-9a0c-0305e82c3301", "Pickup", "Depot", new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), null, null)];

    [Fact]
    public async Task UploadScans_CallerCancels_ThrowsCancellationWithoutRetryOrBanner()
    {
        // Arrange: the fake network never answers on its own.
        var network = new DelayHandler(_ => TimeSpan.FromSeconds(10));
        using var harness = new Harness(network);
        using var cancellation = new CancellationTokenSource();

        // Act: start the call, wait until the request really reached the fake network, then cancel.
        var call = harness.Client.UploadScansAsync("APX-12345678", "courier-007", Scans, cancellation.Token);
        Assert.True(SpinWait.SpinUntil(() => network.Calls == 1, TimeSpan.FromSeconds(5)), "The request never reached the fake network.");
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, network.Calls);   // cancelled, not retried
        Assert.False(harness.Connection.IsServerUnavailable);
    }
}