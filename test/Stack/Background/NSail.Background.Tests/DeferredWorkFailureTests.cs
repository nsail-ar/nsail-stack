// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.Background.Tests;

/// <summary>What a vendor that answers no costs the operation that queued the send: nothing.
/// The request was answered long before the item ran, so the failure has nowhere to be reported
/// to except the metric and the log — and the item queued behind it still goes out, which is
/// the whole reason a notice nobody reads is on a queue at all (nsail#1988, the party
/// invitation).</summary>
public sealed class DeferredWorkFailureTests
{
    [Fact]
    public async Task An_item_that_throws_is_counted_failed_logged_and_the_queue_sends_the_next_one()
    {
        using var meters = new Meters();

        var logs = new LogRecorder();
        var signal = new Signal();

        await using var provider = Harness.Build(services => services.AddDeferredWork(), logs);

        var queue = provider.GetRequiredService<DeferredWork>();

        queue.Enqueue("Tests.Refused", Tenant.None, (_, _) => throw new InvalidOperationException("the vendor on the other end says no"));

        // Enqueued after the failing one, so reaching it at all is the assertion: a runner that
        // let the throw escape its loop would never record this.
        queue.Enqueue("Tests.Accepted", Tenant.None, (_, _) =>
        {
            signal.Record();

            return Task.CompletedTask;
        });

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(1, meters.Items("Tests.Refused", DeferredWorkMetrics.Failed));
        Assert.Equal(0, meters.Items("Tests.Refused", DeferredWorkMetrics.Succeeded));
        Assert.Equal(1, meters.Items("Tests.Accepted", DeferredWorkMetrics.Succeeded));

        Assert.Contains(
            logs.Entries,
            entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }
}
