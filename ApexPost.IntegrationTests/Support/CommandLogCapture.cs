using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// Collects the SQL commands that EF Core executes (the "Executed DbCommand" log entries), so a test can count them.
/// This is how the tests prove there is no N+1 problem: the number of commands must not grow with the number of rows.
/// </summary>
public sealed class CommandLogCapture : ILoggerProvider
{
    private const string Category = "Microsoft.EntityFrameworkCore.Database.Command";
    private readonly List<string> _commands = [];
    private readonly object _gate = new();

    public IReadOnlyList<string> Commands
    {
        get
        {
            lock (_gate)
                return _commands.ToList();
        }
    }

    public void Clear()
    {
        lock (_gate)
            _commands.Clear();
    }

    public ILogger CreateLogger(string categoryName) =>
        categoryName == Category ? new CommandLogger(this) : NullLoggerInstance.Instance;

    public void Dispose()
    {
    }

    private void Add(string message)
    {
        lock (_gate)
            _commands.Add(message);
    }

    private sealed class CommandLogger(CommandLogCapture owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == RelationalEventId.CommandExecuted.Id)
                owner.Add(formatter(state, exception));
        }
    }

    private sealed class NullLoggerInstance : ILogger
    {
        public static readonly NullLoggerInstance Instance = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
