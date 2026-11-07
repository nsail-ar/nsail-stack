// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>What is open inside a hosted dialog and answers Escape itself. The dialog's Escape is
/// the vendor's native listener, which fires before any Blazor handler and which no
/// <c>@onkeydown:stopPropagation</c> reaches, so the dialog withdraws the key while anything here
/// is held (NsOpenDialog).</summary>
internal sealed class EscapeHolds(Func<Task> changed)
{
    readonly HashSet<object> _holders = [];

    public bool Any => _holders.Count > 0;

    /// <summary>Told after every render of a holder; only the edge between nothing held and
    /// something held reaches the dialog.</summary>
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
