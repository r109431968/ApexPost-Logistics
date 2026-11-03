using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Abstractions;

/// <summary>
/// Sends one scan to the central system. In Part A this is a fake; Part D replaces it with the real API call.
/// </summary>
public interface IShipmentUploader
{
    /// <returns>true when the central system accepted the scan; false when it rejected it.</returns>
    Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default);
}
