namespace ApexPost.Courier.Offline.Storage;

public sealed class ScansChangedEventArgs(IReadOnlyList<string> documentIds) : EventArgs
{
    /// <summary>Ids of the documents that were created, updated or deleted.</summary>
    public IReadOnlyList<string> DocumentIds { get; } = documentIds;
}
