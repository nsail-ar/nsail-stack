// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Storage.Git;
using System.Diagnostics;

namespace NSail.Storage.Tests;

// UnobservedTaskException is raised for any task in the process, so this fact runs alone,
// finalizing what the rest of the suite left behind before it listens: what it records is this
// call's.
[CollectionDefinition(nameof(GitCancellationTests), DisableParallelization = true)]
public sealed class GitCancellationCollection
{
}

[Collection(nameof(GitCancellationTests))]
public sealed class GitCancellationTests
{
    readonly GitStorageProvider _provider = new();

    [Fact]
    public async Task ACancelledCallKillsGitAndLeavesNoReadUnobserved()
    {
        using var silent = new Loopback(null);
        var unobserved = new List<Exception>();
        EventHandler<UnobservedTaskExceptionEventArgs> record = (_, args) =>
        {
            lock (unobserved)
            {
                unobserved.Add(args.Exception);
            }
        };

        CollectGarbage();

        TaskScheduler.UnobservedTaskException += record;

        try
        {
            using var cancel = new CancellationTokenSource();
            var listing = _provider.List(GitStorageTests.Credentialed(silent), null, cancel.Token);

            // Cut once the server holds git's request, so what is cancelled is a read off the network.
            await silent.Held.WaitAsync(TimeSpan.FromSeconds(60));

            var clock = Stopwatch.StartNew();

            cancel.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listing);

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"the call outlived its cancellation by {clock.Elapsed}");

            CollectGarbage();

            Assert.Empty(unobserved);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= record;
        }
    }

    static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
