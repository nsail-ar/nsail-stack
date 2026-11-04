// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// The harness's own record, one file a day beside the jobs' logs: a service or a hidden console
// has nobody reading its stdout, and a pass that failed or a lock it released must still be readable.
public sealed class FileLog(string directory) : ILoggerProvider
{
    readonly Lock _gate = new();

    public ILogger CreateLogger(string category)
    {
        return new Writer(this, category[(category.LastIndexOf('.') + 1)..]);
    }

    public void Dispose()
    {
    }

    void Write(string line)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, $"harness-{DateTimeOffset.UtcNow:yyyyMMdd}.log"), line + Environment.NewLine);
        }
    }

    sealed class Writer(FileLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        {
            if (!IsEnabled(level)) return;
            var line = $"{DateTimeOffset.UtcNow:HH:mm:ss} {level.ToString()[..4].ToUpperInvariant()} {category}: {format(state, error)}";
            if (error is not null) line += Environment.NewLine + error;
            try
            {
                log.Write(line);
            }
            catch (IOException)
            {
                // A log line lost to a locked file must never take a pass down with it.
            }
        }
    }
}
