namespace ApexPost.Client;

// The generated client (Generated/ApexPostClient.g.cs) is never edited. Anything we want to add lives in a partial class or in a wrapper like this.

/// <summary>Small helpers on top of the generated client.</summary>
public static class ApexPostClientExtensions
{
    /// <summary>Builds the id that a courier's phone gives a scan: <c>scan::{guid}</c>.</summary>
    public static string NewScanId() => $"scan::{Guid.NewGuid()}";
}
