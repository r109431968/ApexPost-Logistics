namespace ApexPost.Application.Services;

/// <summary>
/// One process-wide gate so two requests cannot change stock at the same time.
/// A real database transaction replaces this next week.
/// </summary>
internal static class StockGate
{
    public static readonly SemaphoreSlim Gate = new(1, 1);
}
