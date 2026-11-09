using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Sync;

/// <summary>What happened to one scan in an upload.</summary>
public enum ScanOutcomeKind
{
    /// <summary>The central system has the scan (newly stored, or it already had it). The scan becomes Synced.</summary>
    Synced,

    /// <summary>The central system refused the scan and sending it again will not help. The scan becomes Failed.</summary>
    Rejected,

    /// <summary>No decision: offline, timeout, server error or circuit open. The scan stays Pending.</summary>
    RetryLater,

    /// <summary>The access token is no longer valid. The scan stays Pending and the user must sign in again.</summary>
    Unauthorized
}

/// <param name="ScanId">The id of the scan (scan::{guid}).</param>
/// <param name="Kind">The result.</param>
/// <param name="Reason">Why, for Rejected and RetryLater. Shown on the Sync Status screen.</param>
public sealed record ScanOutcome(string ScanId, ScanOutcomeKind Kind, string? Reason = null);

/// <summary>
/// Sends a batch of scans to the central system and says what happened to each scan.
/// It must not throw for network problems; it answers <see cref="ScanOutcomeKind.RetryLater"/> instead.
/// </summary>
public interface IScanBatchUploader
{
    Task<IReadOnlyList<ScanOutcome>> UploadBatchAsync(IReadOnlyList<ShipmentScan> batch, CancellationToken cancellationToken = default);
}
