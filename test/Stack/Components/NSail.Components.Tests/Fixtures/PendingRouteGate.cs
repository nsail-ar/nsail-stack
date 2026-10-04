// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Components;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A gate that has not answered yet, and answers when the test says so — the
/// in-flight half of IRouteGate's contract, which FakeRouteGate (already settled) and
/// ClosedRouteGate (already decided) both skip. It is what holds NsRouteGate on its empty
/// frame for as long as a test needs to look at it.</summary>
public sealed class PendingRouteGate : IRouteGate
{
    readonly TaskCompletionSource<Type?> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void LetThrough()
    {
        _answer.TrySetResult(null);
    }

    public Task<Type?> GetRedirect(Type page, CancellationToken cancellationToken = default)
    {
        return _answer.Task;
    }
}
