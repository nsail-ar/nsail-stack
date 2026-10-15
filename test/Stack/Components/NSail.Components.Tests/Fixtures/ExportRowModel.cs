// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

public enum ExportRowKind
{
    Person,
    Shop
}

/// <summary>A row of several shapes, so a column's format has something to be derived from: text,
/// a figure, a date, an enum, and the two a CLR type cannot tell apart (an amount and a share are
/// both decimals).</summary>
public sealed class ExportRowModel
{
    public string? Name { get; set; }

    public int Count { get; set; }

    public decimal Total { get; set; }

    public decimal Share { get; set; }

    public DateOnly On { get; set; }

    public ExportRowKind Kind { get; set; }
}
