// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>
/// How each named surface's current open landed in browser history, for one running app.
/// The surface that opens an overlay and the one that closes it are never the same object —
/// the open is written by the surface underneath, and the overlay's own context is keyed by
/// the route inside it (NsSurface), so it is torn down and rebuilt whenever that route moves
/// — which is why the answer lives beside them rather than inside either.
///
/// Nothing recorded means nothing was pushed, and that is the whole of the deep-link case: an
/// address pasted fresh or a reload starts this service empty, so the surface it arrives with
/// closes by rewriting the address and Back never leaves the app. Nothing is cleared on close
/// either — an entry stays true for as long as it stays in the browser's stack, so a surface
/// that comes back through Forward still knows how it got there.
/// </summary>
public sealed class SurfaceHistory
{
    readonly HashSet<string> _pushed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records how a surface was just opened, replacing what its previous open
    /// recorded: a name is one surface, so it holds one open at a time.</summary>
    public void Opened(Surface surface, bool pushed)
    {
        ArgumentException.ThrowIfNullOrEmpty(surface.Name);

        if (pushed)
        {
            _pushed.Add(surface.Name);
            return;
        }

        _pushed.Remove(surface.Name);
    }

    /// <summary>Whether closing <paramref name="surface"/> pops the entry its open pushed
    /// instead of rewriting the address in place.</summary>
    public bool Pops(Surface surface)
    {
        ArgumentException.ThrowIfNullOrEmpty(surface.Name);

        return _pushed.Contains(surface.Name);
    }
}
