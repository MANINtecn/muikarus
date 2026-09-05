using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Client.Main.Helpers
{
    public static class OnScreenLogger
    {
        private static readonly ConcurrentQueue<LogEntry> _entries = new();
        public const int MaxEntries = 12;

        public struct LogEntry
        {
            public DateTime Timestamp;
            public LogLevel Level;
            public string Category;
            public string Message;
        }

        public static event Action<string, LogLevel, string> OnLogged;

        public static void Log(string message, LogLevel level = LogLevel.Information, string category = "App")
        {
            if (string.IsNullOrEmpty(message)) return;

            // Trim very long messages to 95 chars so they fit nicely on mobile screens
            if (message.Length > 95)
                message = message.Substring(0, 92) + "...";

            _entries.Enqueue(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                Message = message
            });

            while (_entries.Count > MaxEntries)
            {
                _entries.TryDequeue(out _);
            }

            try
            {
                OnLogged?.Invoke(message, level, category);
            }
            catch { }
        }

        public static LogEntry[] GetEntries()
        {
            return _entries.ToArray();
        }
    }

    public class OnScreenLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new OnScreenInternalLogger(categoryName);
        }

        public void Dispose() { }

        private class OnScreenInternalLogger : ILogger
        {
            private readonly string _category;
            public OnScreenInternalLogger(string category) => _category = category;

            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                string msg = formatter != null ? formatter(state, exception) : state?.ToString();
                if (exception != null) msg = $"{msg} [EX: {exception.Message}]";
                OnScreenLogger.Log(msg, logLevel, _category);
            }
        }
    }
}
