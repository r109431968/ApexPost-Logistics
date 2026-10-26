using ApexPost.Application.DTOs;

namespace ApexPost.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Append-only: there is intentionally no update or delete.</summary>
public interface IAuditStore
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken);
}

public interface ICourierTrackingService
{
    Task<TrackingResult> GetStatusAsync(string trackingNumber, CancellationToken cancellationToken);
}

public interface INotificationSink
{
    void Send(Notification notification);

    IReadOnlyList<Notification> Recent(int count);
}

public interface ICredentialStore
{
    Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken);

    Task SetHashAsync(Guid staffId, string hash, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string storedHash);

    /// <summary>A valid hash of a throw-away password, used so unknown accounts cost the same time as known ones.</summary>
    string DummyHash { get; }
}
