// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json.Serialization;

namespace NSail.Paging;

/// <summary>Which rows of a paged list an act is aimed at: the ones named by id, or every row
/// the list's own filter answers — the "select all N" a paged grid offers once a whole page is
/// ticked, which reaches rows no page on screen has shown. The filter itself is not here: it is
/// the list message's own, and the act that carries a selection carries that filter beside it,
/// so the rows are resolved on the server by the same query that listed them.</summary>
public sealed class Selection
{
    public bool All { get; set; }

    public List<Guid> Ids { get; set; } = [];

    [JsonIgnore]
    public bool IsEmpty
    {
        get { return !All && Ids.Count == 0; }
    }

    /// <summary>How many rows the act reaches, given how many the filter answers.</summary>
    public int Count(int total)
    {
        return All ? total : Ids.Count;
    }

    public bool Contains(Guid id)
    {
        return All || Ids.Contains(id);
    }

    public void Clear()
    {
        All = false;
        Ids.Clear();
    }
}
