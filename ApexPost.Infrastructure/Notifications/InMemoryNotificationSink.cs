using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Notifications;

/// <summary>Simulated notifications: kept in memory (last 200) and written to the log.</summary>
public sealed class InMemoryNotificationSink(ILogger<InMemoryNotificationSink> logger) : INotificationSink
{
    private const int Capacity = 200;

    private readonly Lock _gate = new();
    private readonly List<Notification> _items = [];

    public void Send(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        lock (_gate)
        {
            _items.Add(notification);
            if (_items.Count > Capacity)
                _items.RemoveAt(0);
        }

        logger.LogInformation(
            "Notification {Kind}: {Title} - {Message}", notification.Kind, notification.Title, notification.Message);
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<Notification> Recent(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        lock (_gate)
        {
            return _items.AsEnumerable().Reverse().Take(count).ToList();
        }
    }
}
