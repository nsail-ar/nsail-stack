// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributes actions to an outlet — the typed slot a projection's owner founds
/// (PartyRowOutlet for Directory's Parties grid). The contributor reads the outlet and
/// returns: it never operates the component it was handed (no StateHasChanged, no keeping
/// the reference — "something changed" is a published event), and it does no I/O per call,
/// because an outlet renders once per row.</summary>
public interface IActionContributor<in TOutlet>
    where TOutlet : IActionOutlet
{
    Task<IReadOnlyList<ActionItem>> GetActions(TOutlet outlet);
}
