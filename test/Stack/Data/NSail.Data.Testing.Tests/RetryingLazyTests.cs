// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Data.Testing;
using Xunit;

namespace NSail.Data.Testing.Tests;

public class RetryingLazyTests
{
    // The story this class exists to close: TestDatabase's Lazy<Task> cached the first
    // sweep's fault forever, so one transient Postgres hiccup failed a whole assembly. A
    // factory that throws once and then succeeds is the smallest reproduction of that
    // hiccup, without touching Postgres at all.
    [Fact]
    public async Task AFactoryThatThrowsOnceThenSucceedsRecoversOnTheNextAccess()
    {
        var attempts = 0;

        var lazy = new RetryingLazy(async () =>
        {
            attempts++;

            if (attempts == 1)
            {
                throw new InvalidOperationException("Transient hiccup.");
            }

            await Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => lazy.Value);

        await lazy.Value;

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ASuccessfulFactoryRunsOnlyOnceAndIsSharedByEveryCaller()
    {
        var attempts = 0;

        var lazy = new RetryingLazy(async () =>
        {
            attempts++;

            await Task.CompletedTask;
        });

        await lazy.Value;
        await lazy.Value;
        await lazy.Value;

        Assert.Equal(1, attempts);
    }

    // Sharing between callers that arrive one after the other is not the promise the callers
    // actually lean on: TestDatabase's sweep and the Optical E2E harness's Release build are
    // both asked for by fixtures xunit starts in parallel, so the second caller arrives while
    // the first factory run is still in flight. That is the case where a factory running twice
    // means two sweeps, or two builds over one obj/ tree (nsail#450).
    [Fact]
    public async Task ACallerThatArrivesWhileTheFactoryIsStillRunningSharesThatRun()
    {
        var attempts = 0;
        var started = new TaskCompletionSource();
        var finish = new TaskCompletionSource();

        var lazy = new RetryingLazy(async () =>
        {
            attempts++;

            started.SetResult();

            await finish.Task;
        });

        var first = lazy.Value;

        await started.Task;

        var others = new[] { lazy.Value, lazy.Value, lazy.Value };

        finish.SetResult();

        await first;
        await Task.WhenAll(others);

        Assert.Equal(1, attempts);
        Assert.All(others, other => Assert.Same(first, other));
    }

    [Fact]
    public async Task ARepeatedlyFailingFactoryKeepsFailing()
    {
        var attempts = 0;

        var lazy = new RetryingLazy(async () =>
        {
            attempts++;

            await Task.CompletedTask;

            throw new InvalidOperationException("Still down.");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => lazy.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lazy.Value);

        Assert.Equal(2, attempts);
    }
}
