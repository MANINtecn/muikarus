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

        // Full session log (not trimmed to 12 lines) so it can be shared from the device.
        private static readonly object _fullLock = new();
        private static readonly System.Collections.Generic.List<string> _fullLog = new();
        private const int MaxFullLines = 4000;
        private static string _logFilePath;

        /// <summary>Set by the platform layer (Android) to share the log text (WhatsApp/Telegram/etc).</summary>
        public static Action<string> ShareLogRequested;

        public static void Log(string message, LogLevel level = LogLevel.Information, string category = "App")
        {
            if (string.IsNullOrEmpty(message)) return;

            AppendFull(message, level, category);

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

        private static void AppendFull(string message, LogLevel level, string category)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] [{category}] {message}";
            lock (_fullLock)
            {
                _fullLog.Add(line);
                if (_fullLog.Count > MaxFullLines)
                    _fullLog.RemoveRange(0, _fullLog.Count - MaxFullLines);

                try
                {
                    if (_logFilePath == null)
                    {
                        _logFilePath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "ikarus_log.txt");
                        System.IO.File.WriteAllText(_logFilePath, $"=== Ikarus MU log {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                    }
                    System.IO.File.AppendAllText(_logFilePath, line + "\n");
                }
                catch { /* log file is best-effort */ }
            }
        }

        /// <summary>Returns the last <paramref name="maxChars"/> characters of the session log.</summary>
        public static string GetFullLogText(int maxChars = 120000)
        {
            string text;
            lock (_fullLock)
            {
                text = string.Join("\n", _fullLog);
            }
            if (text.Length > maxChars) text = text.Substring(text.Length - maxChars);
            return text;
        }

        public static void ShareLog()
        {
            var handler = ShareLogRequested;
            if (handler == null)
            {
                Log("[LOG] Compartilhar log indisponivel nesta plataforma.", LogLevel.Warning);
                return;
            }
            handler(GetFullLogText());
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
