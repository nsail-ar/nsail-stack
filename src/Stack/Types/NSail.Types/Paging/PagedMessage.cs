// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Paging;

/// <summary>What a message asking for a <see cref="DataPage{T}"/> declares about the page it
/// wants, once instead of on every list. Zero page size keeps meaning "the caller did not
/// choose" — the handler's own default answers it — so what the bounds refuse is only what no
/// legitimate caller sends: a negative index and a read that is an export rather than a page.
/// </summary>
public abstract class PagedMessage
{
    /// <summary>The largest page any caller in the tree asks for: a calendar reading a whole
    /// agenda. Past it the caller wants an export, which is a message of its own.</summary>
    public const int MaxPageSize = 1000;

    [Range(0, int.MaxValue)]
    public int PageIndex { get; set; }

    [Range(0, MaxPageSize)]
    public int PageSize { get; set; } = 10;
}
