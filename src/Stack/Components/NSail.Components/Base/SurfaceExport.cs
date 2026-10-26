// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Exporting;

namespace NSail.Components;

/// <summary>A reading grid's export as its surface holds it: the trigger chrome draws, the file
/// kinds this install can write, and the act itself.
/// <para>One announcement and not two, because a trigger a header draws and an act something
/// else runs are the SAME list's export: announced apart they could name two grids, and the
/// slot's whole point is that there is one list the surface exports.</para>
/// <para><see cref="Run"/> is what a caller that is not the person at the trigger needs — an
/// agent asked for the file of a list nobody is looking at — and it answers how many rows the
/// file carries, or null where no file was written at all: a read that does not land leaves
/// none (exporting.md), and nothing else would tell a caller that away from the screen.</para></summary>
public sealed class SurfaceExport
{
    public required RenderFragment Trigger { get; init; }

    /// <summary>What this install registered, in <see cref="ExportKind"/>'s own order — the same
    /// list the trigger offers. A kind not here has no writer, so <see cref="Run"/> writes no
    /// file for it rather than refusing: what an install can produce is what it registered.</summary>
    public required IReadOnlyList<ExportKind> Kinds { get; init; }

    public required Func<ExportKind, Task<int?>> Run { get; init; }
}
