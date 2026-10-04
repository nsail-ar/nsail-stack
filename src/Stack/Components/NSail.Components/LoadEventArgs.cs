// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Resolves the item behind an already-bound value (autocomplete OnLoad: the
/// edit form has the id, the handler supplies the item to display).</summary>
public sealed class LoadEventArgs<TItem, TValue> : AsyncEventArgs
{
    public required TValue Value { get; init; }

    public TItem? Item { get; set; }
}
