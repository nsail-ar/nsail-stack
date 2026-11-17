// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// The machine's workspace could not be prepared: every job its worker takes would break the same way,
// whatever story it is for.
internal sealed class WorkspaceFault(string step, Exception inner) : Exception($"{step}: {inner.Message}", inner)
{
    public string Step { get; } = step;
}
