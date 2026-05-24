using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Logging;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;
    private readonly LogLevel _minimumLevel;
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public FileLoggerProvider(string logPath, LogLevel minimumLevel)
    {
        _logPath = logPath;
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _minimumLevel, WriteLine));

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }

    private void WriteLine(string line)
    {
        if (_disposed)
            return;

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel _minimumLevel;
        private readonly Action<string> _writeLine;

        public FileLogger(string categoryName, LogLevel minimumLevel, Action<string> writeLine)
        {
            _categoryName = categoryName;
            _minimumLevel = minimumLevel;
            _writeLine = writeLine;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= _minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
                return;

            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            var line = $"{timestamp} [{logLevel}] {_categoryName}: {message}";
            _writeLine(line);

            if (exception is not null)
                _writeLine(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
