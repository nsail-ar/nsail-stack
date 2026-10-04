// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A contributor that answers only when the test says so — the slow one, standing in
/// for a verb that has to ask something across a network before it knows whether the row
/// carries it. One gate per ask: an outlet whose Context moves before the first answer has two
/// of them in flight, and which one answers first is the whole question there.</summary>
public sealed class WaitingContributor : IActionContributor<ProbeRowOutlet>
{
    readonly List<TaskCompletionSource> _asks = [];

    public required string Verb { get; init; }

    public int Asks
    {
        get { return _asks.Count; }
    }

    public void Answer(int ask = 0)
    {
        _asks[ask].TrySetResult();
    }

    public async Task<IReadOnlyList<ActionItem>> GetActions(ProbeRowOutlet outlet)
    {
        ArgumentNullException.ThrowIfNull(outlet);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _asks.Add(gate);

        // Read before the wait: the outlet instance survives a Context change, so an answer
        // that reads it afterwards would describe a model this ask was never about.
        var context = outlet.Context;

        await gate.Task;

        return [new ActionItem { Name = $"{Verb}-{context}", OnClick = () => Task.CompletedTask }];
    }
}

/// <summary>A contributor that already knows — no I/O, no yield, the case the mark must never
/// appear for.</summary>
public sealed class SettledContributor : IActionContributor<ProbeRowOutlet>
{
    public required string Verb { get; init; }

    public Task<IReadOnlyList<ActionItem>> GetActions(ProbeRowOutlet outlet)
    {
        ArgumentNullException.ThrowIfNull(outlet);

        return Task.FromResult<IReadOnlyList<ActionItem>>(
        [
            new ActionItem { Name = $"{Verb}-{outlet.Context}", OnClick = () => Task.CompletedTask },
        ]);
    }
}
