// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

public abstract class DialogManager
{
    public abstract Task<bool> Confirm(string message, string? title = null);

    public abstract Task Alert(string message, string? title = null);

    public abstract void Notify(string message, NsSeverity severity = NsSeverity.Info);

    /// <summary>A notice that waits instead of fading: it stays until the operator takes the
    /// one action on it or closes it, and it blocks nothing meanwhile. For what has to be
    /// offered and never forced — the reader picks the moment, over whatever they are in the
    /// middle of.</summary>
    public abstract void Offer(string message, string label, Action accepted);

    /// <summary>Opens a component in a modal dialog and completes when it closes. The
    /// component is ordinary hosted content — it receives a cascaded SurfaceContext whose
    /// Close() closes the dialog; results flow through its own EventCallback parameters.</summary>
    public abstract Task Open<TComponent>(
        string? title = null,
        IReadOnlyDictionary<string, object?>? parameters = null)
        where TComponent : IComponent;
}
