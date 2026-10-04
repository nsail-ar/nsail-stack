// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A contributor whose answer is still in flight — the shape of a kit asking its own
/// server. Holding it open is the only way to put two parameter sets inside one build, which is
/// where a memo that is merely likely stops being one.</summary>
public sealed class HeldNavContributor(params NavMenuItem[] items) : INavMenuContributor
{
    readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Calls { get; private set; }

    public async Task<IReadOnlyList<NavMenuItem>> GetItems()
    {
        Calls++;

        await _held.Task;

        return items;
    }

    public void Answer()
    {
        _held.TrySetResult();
    }
}
