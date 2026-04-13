using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Servercyde.Monitoring.Tests.Fakes;

public class FakeLogger<T> : ILogger<T>
{
    public IList<LogEntry> LogEntries { get; } = [];
    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        return NullLogger.Instance.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add(new LogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            State = state,
            Exception = exception,
            Message = formatter(state, exception),
            Timestamp = DateTime.UtcNow

        });
    }
}

public class FakeLoggerFactory : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName)
    {
        return new FakeLogger<object>();
    }
    public void Dispose() 
    {
        GC.SuppressFinalize(this);
    }
    public void AddProvider(ILoggerProvider provider) { }
}

public class LogEntry
{
    public LogLevel LogLevel { get; set; }

    public EventId EventId { get; set; }

    public string? Message { get; set; }

    public Exception? Exception { get; set; }

    public object? State { get; set; }

    public DateTime Timestamp { get; set; }
}


