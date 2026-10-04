// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public sealed class SubmitEventArgs<TModel> : AsyncEventArgs
{
    public required TModel Model { get; init; }

    /// <summary>Whether the handler returned without carrying the submit through — a choice
    /// the form still needs, a confirmation the user declined. It is the handler's only way to
    /// say so: nothing was refused and nothing failed, so the form would otherwise read the
    /// return as success and finish the surface over a save that never happened.</summary>
    public bool Aborted { get; private set; }

    /// <summary>Ends this submit without success (see Aborted).</summary>
    public void Abort()
    {
        Aborted = true;
    }
}
