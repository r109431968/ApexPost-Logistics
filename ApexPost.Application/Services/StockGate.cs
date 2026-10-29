using System.Collections.Concurrent;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

/// <summary>
/// One lock per branch, so two requests cannot change the same branch's stock at the same time,
/// while payments at different branches never wait for each other.
/// A real database transaction replaces this next week.
/// </summary>
internal static class StockGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public static SemaphoreSlim For(BranchCode branch) =>
        Gates.GetOrAdd(branch.Value, _ => new SemaphoreSlim(1, 1));
}