// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Base for the Stack's collection chrome — the hosts that draw a way IN to a
/// collection rather than a value: the bar's `+`, the acts beside it, a row's own editor and
/// its edit/delete controls (NsTable, NsListEditor).
///
/// What a read-only form must take away here is drawn by the host, not by the screen that
/// handed it an editor — so the host is where the cascade is read, and no caller repeats the
/// rule (ui/hosts.md). The read itself is NsActBase's, shared with the acts a kit draws for
/// itself beside this chrome.</summary>
public abstract class NsCollectionBase : NsActBase
{
}
