// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Paging;

public sealed record DataPage<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageIndex { get; init; }

    public int PageSize { get; init; } = 10;

    public int TotalCount { get; init; }

    public static DataPage<T> Empty(int pageSize = 10)
    {
        return new() { PageIndex = 0, PageSize = pageSize };
    }

    public static DataPage<T> From(IReadOnlyList<T> items)
    {
        var count = items.Count;

        return new DataPage<T>
        {
            Items = items,
            PageIndex = 0,
            PageSize = count == 0 ? 10 : count,
            TotalCount = count
        };
    }

    public static implicit operator DataPage<T>(T[] items)
    {
        return From(items);
    }

    public static implicit operator DataPage<T>(List<T> items)
    {
        return From(items);
    }
}
