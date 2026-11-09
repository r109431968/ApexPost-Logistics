using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;

namespace ApexPost.Courier.Core.Sync;

/// <summary>
/// Sends a batch of scans to ApexPost.Api and says what happened to each scan.
/// The API takes the scans of one shipment per request, so a batch (up to 20 scans) is split by tracking number and courier.
/// What each answer means for a scan:
/// - 200: the scans that are not in "rejected" are Synced (newly stored or already there = duplicate, both are fine);
///   the rejected ones are Rejected.
/// - 400, 403, 404, 409: a decision about the data. Sending it again gives the same answer, so the scans are Rejected.
/// - 401: the token is no longer good. The scans stay Pending (Unauthorized).
/// - offline, timeout, circuit open, 5xx, 429: no decision. The scans stay Pending (RetryLater) and the rest of the batch is not tried.
/// </summary>
public sealed class ApiScanBatchUploader(ApexApiClient api) : IScanBatchUploader
{
    public async Task<IReadOnlyList<ScanOutcome>> UploadBatchAsync(IReadOnlyList<ShipmentScan> batch, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<ScanOutcome>(batch.Count);
        ScanOutcome? stopOutcome = null;   // set when the rest of the batch must not be tried

        foreach (var group in batch.GroupBy(scan => (scan.TrackingNumber, scan.CourierId)))
        {
            if (stopOutcome is not null)
            {
                outcomes.AddRange(group.Select(scan => stopOutcome with { ScanId = scan.Id }));
                continue;
            }

            var items = group.Select(ToDto).ToList();
            var result = await api.UploadScansAsync(group.Key.TrackingNumber, group.Key.CourierId, items, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                var rejected = result.Value!.Rejected.ToDictionary(item => item.ScanId, item => item.Reason);
                outcomes.AddRange(group.Select(scan => rejected.TryGetValue(scan.Id, out var reason)
                    ? new ScanOutcome(scan.Id, ScanOutcomeKind.Rejected, reason)
                    : new ScanOutcome(scan.Id, ScanOutcomeKind.Synced)));
                continue;
            }

            var error = result.Error!;
            if (error.Kind == ApiFailureKind.Unauthorized)
            {
                stopOutcome = new ScanOutcome("", ScanOutcomeKind.Unauthorized, "Sign in again.");
                outcomes.AddRange(group.Select(scan => stopOutcome with { ScanId = scan.Id }));
            }
            else if (error.IsTransient || error.Kind == ApiFailureKind.Unexpected)
            {
                stopOutcome = new ScanOutcome("", ScanOutcomeKind.RetryLater, error.Message);
                outcomes.AddRange(group.Select(scan => stopOutcome with { ScanId = scan.Id }));
            }
            else
            {
                outcomes.AddRange(group.Select(scan => new ScanOutcome(scan.Id, ScanOutcomeKind.Rejected, error.Message)));
            }
        }

        return outcomes;
    }

    private static ScanItemDto ToDto(ShipmentScan scan) => new(
        scan.Id,
        scan.ScanType.ToString(),
        scan.Location,
        scan.ScannedAtUtc,
        scan.Notes,
        scan.RecipientSignatureName);
}
