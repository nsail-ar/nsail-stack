// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

public abstract class DialogManager
{
    public abstract Task<bool> Confirm(string message, string? title = null);

    /// <summary>The same question for an act that DESTROYS something — a delete, a draft
    /// abandoned — which differs in one thing and nothing else: where the cursor opens. An
    /// ordinary confirmation opens on the act it offers, so Enter takes it; this one opens on
    /// the way out, so a reflex Enter costs nothing (ui/forms.md, <c>Enter</c>). Which acts
    /// destroy is DERIVED and never asked of a screen — the message's own verb
    /// (NsPartial.ConfirmSend) and the house's own exit guard are the two that know.
    ///
    /// <para>Virtual and not abstract: a host that cannot place a cursor still owes the
    /// question, and asking it is the whole of what a manager with no focus to give can
    /// do.</para></summary>
    public virtual Task<bool> ConfirmDestruction(string message, string? title = null)
    {
        return Confirm(message, title);
    }

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
