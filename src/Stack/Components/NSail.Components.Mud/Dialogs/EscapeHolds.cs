// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// What is open inside a hosted dialog and answers Escape itself. The dialog's Escape is the
// vendor's native listener, which fires before any Blazor handler and which no
// @onkeydown:stopPropagation reaches, so the dialog withdraws the key while anything here is held
// (NsOpenDialog).
internal sealed class EscapeHolds(Func<Task> changed)
{
    readonly HashSet<object> _holders = [];

    public bool Any => _holders.Count > 0;

    // Told after every render of a holder; only the edge between nothing held and something held
    // reaches the dialog.
    public Task Set(object holder, bool holds)
    {
        var before = Any;

        if (holds)
        {
            _holders.Add(holder);
        }
        else
        {
            _holders.Remove(holder);
        }

        return before == Any ? Task.CompletedTask : changed();
    }
}
