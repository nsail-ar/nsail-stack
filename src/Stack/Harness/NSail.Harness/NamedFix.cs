// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// The commits of the default branch an analysis named as already holding a story's work (another
// story's merge fixed it), oldest first, and when it said so.
public sealed record NamedFix(IReadOnlyList<string> Shas, DateTimeOffset At);
