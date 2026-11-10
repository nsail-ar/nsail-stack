// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Board;

// A story as a watcher finds it again: ClosedAt is null while it is open. Muted: it was closed as
// not planned (on GitHub, the close reason), which says the thing it was filed for is not a bug.
public sealed record FiledStory(int Number, string Title, string Body, DateTimeOffset? ClosedAt, bool Muted = false)
{
    public bool Open => ClosedAt is null;
}
