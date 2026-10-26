using ApexPost.Application.Abstractions;

namespace ApexPost.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
