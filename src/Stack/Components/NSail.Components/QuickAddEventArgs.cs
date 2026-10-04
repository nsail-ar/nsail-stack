// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>What the dropdown's escape hatch asks its facade: here is the text nothing matched,
/// answer with the row it stands for. The handler creates it however its kit creates things — the
/// control neither knows nor names that message — and the field selects whatever comes back. An
/// unanswered args (Item left null) means the row was not created, and the field keeps asking.</summary>
public sealed class QuickAddEventArgs<TItem> : AsyncEventArgs
{
    public required string Text { get; init; }

    public TItem? Item { get; set; }
}
