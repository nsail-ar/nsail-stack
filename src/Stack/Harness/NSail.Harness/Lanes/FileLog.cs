// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// The harness's own record, one file a day beside the jobs' logs: a service or a hidden console
// has nobody reading its stdout, and a pass that failed or a lock it released must still be readable.
// With `keep`, the first line of each day drops every log of the folder last written before it.
public sealed class FileLog(string directory, TimeSpan? keep = null) : ILoggerProvider
{
    readonly Lock _gate = new();
    string? _day;

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
            var now = DateTimeOffset.UtcNow;
            var day = $"{now:yyyyMMdd}";
            if (day != _day)
            {
                _day = day;
                Sweep(now);
            }
            File.AppendAllText(Path.Combine(directory, $"harness-{day}.log"), line + Environment.NewLine);
        }
    }

    internal void Sweep(DateTimeOffset now)
    {
        if (keep is not { } age) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < now - age) File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A log still held open is tomorrow's.
            }
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
