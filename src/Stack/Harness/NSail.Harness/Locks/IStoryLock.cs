// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Locks;

// One story, one job at a time, across every harness: what decides who builds a story. The board's
// lock labels are what people see; this is what is atomic.
public interface IStoryLock
{
    // True when this caller now holds the story.
    Task<bool> TryAcquire(int story, LockHolder holder, CancellationToken ct = default);

    Task<LockHolder?> Holder(int story, CancellationToken ct = default);

    Task Release(int story, CancellationToken ct = default);

    // Every story held, with the machine that holds it.
    Task<IReadOnlyDictionary<int, string>> Holders(CancellationToken ct = default);
}
