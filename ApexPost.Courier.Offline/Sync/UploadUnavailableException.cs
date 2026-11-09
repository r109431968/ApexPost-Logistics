namespace ApexPost.Courier.Offline.Sync;

/// <summary>
/// The scan could not be sent because the network or the server is not reachable (offline, timeout, server error, circuit open).
/// This is not a decision about the scan, so the scan stays Pending and is sent again later.
/// </summary>
public sealed class UploadUnavailableException : Exception
{
    public UploadUnavailableException() { }
    public UploadUnavailableException(string message) : base(message) { }
    public UploadUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
