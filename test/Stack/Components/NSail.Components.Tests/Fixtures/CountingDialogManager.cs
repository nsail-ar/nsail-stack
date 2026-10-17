// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Components;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Stands in for the real dialog host so a test can ask the only question that
/// matters about the unsaved-changes guard: was the person asked at all. Alerts, toasts and
/// offers are kept apart rather than counted together — which of the three a message takes is
/// itself the contract (a toast fades, a dialog waits, an offer waits and can be taken).</summary>
public sealed class CountingDialogManager : DialogManager
{
    public int Confirms { get; private set; }

    public bool Answer { get; set; } = true;

    public List<string> Alerts { get; } = [];

    public List<string> Notices { get; } = [];

    public List<OfferedAction> Offers { get; } = [];

    public override Task<bool> Confirm(string message, string? title = null)
    {
        Confirms++;

        return Task.FromResult(Answer);
    }

    public override Task Alert(string message, string? title = null)
    {
        Alerts.Add(message);

        return Task.CompletedTask;
    }

    public override void Notify(string message, NsSeverity severity = NsSeverity.Info)
    {
        Notices.Add(message);
    }

    public override void Offer(string message, string label, Action accepted)
    {
        Offers.Add(new OfferedAction(message, label, accepted));
    }

    public List<OpenedDialog> Opens { get; } = [];

    public override Task Open<TComponent>(
        string? title = null,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        Opens.Add(new OpenedDialog(typeof(TComponent), title, parameters));

        return Task.CompletedTask;
    }
}

/// <summary>An offer as the operator has it: the words, the one thing to press, and what
/// pressing it runs — which is how a test takes it in their place.</summary>
public sealed record OfferedAction(string Message, string Label, Action Accepted);

/// <summary>A hosted dialog as its opener asked for it: which component, its title and what it
/// was handed — the parameters being the question for a sheet whose whole content is read off
/// the screen before it opens.</summary>
public sealed record OpenedDialog(Type Component, string? Title, IReadOnlyDictionary<string, object?>? Parameters);
