// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>What a module offers the drawer's search box besides the map: results of its own
/// for a term the person typed, drawn under whatever entries the typing left. The box filters
/// the menu, which is the list of where a session may GO; this is the seam for what a session
/// is looking FOR — a record the app can name and open — and an app that registers none has
/// the drawer it already had.
///
/// <para>Separate from <see cref="INavMenuContributor"/> for the same reason
/// <see cref="INavMenuCount"/> is: the tree is a function of the session and is asked once for
/// one, while this is asked per term and the term is the whole input. So the implementation is
/// a read, asked again whenever the typing settles, and it owes nothing to whoever asked.</para>
///
/// <para>How many results is the contributor's own answer — the drawer draws what it is handed
/// — and so is who may see them: the read is authorized where it is sent, never by the drawer
/// (permissions.md).</para></summary>
public interface INavMenuSearch
{
    /// <summary>The term as the person typed it, trimmed and never shorter than what
    /// <see cref="NavMenu"/> considers a question. Nothing to offer is an empty list, and that
    /// is also the honest answer when the results cannot be had: a drawer is chrome, so what
    /// failed to arrive is drawn as nothing rather than as an error over the app's own
    /// map.</summary>
    Task<IReadOnlyList<NavMenuResult>> Find(string term);
}
