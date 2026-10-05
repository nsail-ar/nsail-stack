// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Components;

/// <summary>What a form has to say that names no field it renders. The panel footer that
/// holds the form's buttons claims these and seats them in its own row, so a refusal
/// appears beside the act it answers without reserving any geometry of its own; a form
/// nobody claims falls back to drawing them itself, after its content, only when there is
/// something to say.</summary>
public interface IFormProblems
{
    IReadOnlyList<string> Problems { get; }

    /// <summary>The label of the one act a refusal may offer, or null when the refusal on screen
    /// is not one a refetch answers. Only a save the stored row moved out from under earns it:
    /// the values the user typed stay exactly where they are, this goes and gets the newer row,
    /// and re-applying the edit is the user's to do. The form resolves the wording, so a
    /// claimant renders it without knowing where strings come from.</summary>
    string? ReloadAct { get; }

    Task Reload();

    /// <summary>Raised when <see cref="Problems"/> changed — the claimant renders in its own
    /// tree and would not re-render otherwise.</summary>
    event Action? Changed;

    /// <summary>Draws a refusal the form HOSTS but did not run — a page act standing inside it,
    /// whose Runner belongs to the page (a Probar beside the Guardar). Placed by the rule the
    /// form's own refusals are placed by: under the field the issue names when the form renders
    /// that field, at the foot otherwise. Answers whether it drew anything, so a refusal nothing
    /// on screen can carry still reaches the report it would have had.
    /// <para>A null problem lifts what a hosted act left standing, and only that: the act that
    /// works reports nothing at all, so its silence can only be read at the next press — while a
    /// submit's own refusal is never this one's to clear.</para></summary>
    bool Place(Problem? problem);

    void Claim();

    void Release();
}
