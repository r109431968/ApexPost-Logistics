using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Sync;

/// <summary>
/// A stand-in for the real API (Part D replaces it). It accepts every scan, except scans whose tracking number
/// starts with the failure prefix (default "FAIL"), which it rejects. That makes both outcomes easy to demonstrate.
/// </summary>
public sealed class FakeShipmentUploader : IShipmentUploader
{
    private readonly TimeSpan _delay;
    private readonly string _failTrackingPrefix;

    public FakeShipmentUploader(TimeSpan? delay = null, string failTrackingPrefix = "FAIL")
    {
        ArgumentException.ThrowIfNullOrEmpty(failTrackingPrefix);
        _delay = delay ?? TimeSpan.Zero;
        _failTrackingPrefix = failTrackingPrefix;
    }

    public async Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);

        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        }

        return !scan.TrackingNumber.StartsWith(_failTrackingPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
