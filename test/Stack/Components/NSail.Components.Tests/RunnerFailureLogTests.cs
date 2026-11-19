// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using NSail.Problems;
using Xunit;

namespace NSail.Components.Tests;

// The person reads only "Something went wrong" for a run that threw, so the cause survives in the
// log or nowhere. A refusal is drawn by name and a run's own cancellation is nobody's failure:
// neither is a fault, so neither logs at Error.
public sealed class RunnerFailureLogTests
{
    [Fact]
    public async Task A_run_that_throws_logs_the_exception_at_error_and_still_reports_unhandled()
    {
        var host = new Host();
        var log = new Capture();
        var runner = new Runner(host, log);
        var thrown = new InvalidOperationException("boom");

        await runner.Run(() => throw thrown);

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(thrown, entry.Exception);

        var reported = Assert.Single(host.Reports);
        Assert.Equal(SystemProblem.Unhandled().Code, reported.Problem.Code);
        Assert.Same(thrown, reported.Exception);
    }

    // The shape that hid behind the banner: a submit that reloads its own form asks the Runner it
    // is running on for a second run.
    [Fact]
    public async Task A_run_that_reenters_its_own_runner_logs_the_reentry()
    {
        var log = new Capture();
        var runner = new Runner(new Host(), log);

        await runner.Run(() => runner.Run(() => Task.CompletedTask));

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        var exception = Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal("Runner is already running.", exception.Message);
    }

    [Fact]
    public async Task A_refusal_is_reported_by_name_and_logs_nothing()
    {
        var host = new Host();
        var log = new Capture();
        var runner = new Runner(host, log);
        var refusal = new Problem(code: "Refused", title: "Refused", issues: [], status: null);

        await runner.Run(() => throw new BusinessException(refusal));

        Assert.Empty(log.Entries);
        Assert.Equal("Refused", Assert.Single(host.Reports).Problem.Code);
    }

    [Fact]
    public async Task A_run_cancelled_by_its_own_token_logs_nothing()
    {
        var host = new Host();
        var log = new Capture();
        var runner = new Runner(host, log);

        await runner.Run(cancellationToken =>
        {
            runner.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        });

        Assert.Empty(log.Entries);
        Assert.Empty(host.Reports);
    }

    [Fact]
    public async Task A_run_asked_after_the_component_ended_logs_nothing()
    {
        var log = new Capture();
        var runner = new Runner(new Host(), log);

        runner.Dispose();

        await runner.Run(() => throw new InvalidOperationException());

        Assert.Empty(log.Entries);
    }

    sealed class Host : IRunHost
    {
        public List<ProblemEventArgs> Reports { get; } = [];

        public SurfaceContext? Surface
        {
            get { return null; }
        }

        public object Source
        {
            get { return this; }
        }

        public Task Report(ProblemEventArgs args)
        {
            Reports.Add(args);

            return Task.CompletedTask;
        }

        public void StateChanged()
        {
        }
    }

    sealed class Capture : ILogger
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception));
        }
    }
}
