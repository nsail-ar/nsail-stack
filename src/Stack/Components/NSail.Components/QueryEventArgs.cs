// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public sealed class QueryEventArgs : AsyncEventArgs
{
    public int PageIndex { get; init; }

    public int PageSize { get; init; }
}
