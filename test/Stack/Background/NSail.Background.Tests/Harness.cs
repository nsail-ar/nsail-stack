// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace NSail.Background.Tests;

/// <summary>A counter the scoped jobs share, with a task that completes once it reaches its
/// target. Every wait in these tests is bounded by a signal and asserted on the count —
/// a sleep long enough to "probably" have ticked proves nothing about a cadence.</summary>
sealed class Signal
{
    readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    int _count;

    public int Target { get; init; } = 1;

    public int Count => Volatile.Read(ref _count);

    public Task Reached => _reached.Task;

    public int Record()
    {
        var count = Interlocked.Increment(ref _count);

        if (count >= Target)
        {
            _reached.TrySetResult();
        }

        return count;
    }
}

/// <summary>The scoped instances a job saw, one per run — what proves a tick gets its own
/// scope rather than a singleton carried across ticks.</summary>
sealed class Seen
{
    readonly List<Guid> _ids = [];

    public IReadOnlyList<Guid> Ids
    {
        get
        {
            lock (_ids)
            {
                return _ids.ToList();
            }
        }
    }

    public void Add(Guid id)
    {
        lock (_ids)
        {
            _ids.Add(id);
        }
    }
}

sealed class Scoped
{
    public Guid Id { get; } = Guid.NewGuid();
}

sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>Captures what the runner logged. A provider rather than a fake ILogger&lt;T&gt;:
/// the runner's logger is typed on an internal class, so naming it from here would need the
/// assembly to open itself up for a test.</summary>
sealed class LogRecorder : ILoggerProvider
{
    readonly List<LogEntry> _entries = [];
    readonly List<(Func<LogEntry, bool> Matches, TaskCompletionSource<LogEntry> Reached)> _awaited = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToList();
            }
        }
    }

    /// <summary>A signal for the tests whose subject is what was logged rather than what ran —
    /// a job that correctly runs for nobody records no count to wait on, and a delay long
    /// enough to "probably" have logged proves as little here as it does about a cadence.</summary>
    public Task<LogEntry> Awaits(Func<LogEntry, bool> matches)
    {
        lock (_entries)
        {
            if (_entries.FirstOrDefault(matches) is { } already)
            {
                return Task.FromResult(already);
            }

            var reached = new TaskCompletionSource<LogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);

            _awaited.Add((matches, reached));

            return reached.Task;
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Recorder(this, categoryName);
    }

    public void Dispose()
    {
    }

    void Add(LogEntry entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);

            foreach (var awaited in _awaited.Where(candidate => candidate.Matches(entry)))
            {
                awaited.Reached.TrySetResult(entry);
            }
        }
    }

    sealed class Recorder : ILogger
    {
        readonly LogRecorder _recorder;
        readonly string _category;

        public Recorder(LogRecorder recorder, string category)
        {
            _recorder = recorder;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _recorder.Add(new LogEntry(logLevel, _category, formatter(state, exception), exception));
        }
    }
}

static class Harness
{
    static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    public static ServiceProvider Build(Action<IServiceCollection> configure, LogRecorder? logs = null)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });

        configure(services);

        return services.BuildServiceProvider();
    }

    /// <summary>Starts the runner, waits for the signal or the bound, and stops it. Stopping
    /// is awaited rather than fired: BackgroundService.StopAsync joins the loops, so the
    /// assertions that follow read a counter nothing is still writing to.</summary>
    public static async Task Run(ServiceProvider provider, Task signal)
    {
        var runner = provider.GetServices<IHostedService>().Single();

        await runner.StartAsync(CancellationToken.None);

        var finished = await Task.WhenAny(signal, Task.Delay(_bound));

        await runner.StopAsync(CancellationToken.None);

        Assert.True(ReferenceEquals(finished, signal), $"the job did not signal within {_bound.TotalSeconds} seconds.");
    }

    /// <summary>The same bounded start-and-stop for what an observable instrument says. A gauge
    /// records nothing on its own — it is asked — so the wait is bounded by re-reading it rather
    /// than by a signal the job files, and a sleep long enough for the number to "probably" have
    /// moved proves as little here as it does about a cadence.</summary>
    public static async Task Run(ServiceProvider provider, Func<bool> reached)
    {
        var runner = provider.GetServices<IHostedService>().Single();

        await runner.StartAsync(CancellationToken.None);

        var elapsed = Stopwatch.StartNew();

        while (!reached() && elapsed.Elapsed < _bound)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        var met = reached();

        await runner.StopAsync(CancellationToken.None);

        Assert.True(met, $"the instruments did not reach the expected reading within {_bound.TotalSeconds} seconds.");
    }
}
