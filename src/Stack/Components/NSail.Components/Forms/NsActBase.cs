// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

/// <summary>Base for what a form's writability reaches outside its fields: the chrome that
/// offers a way IN rather than a value (NsCollectionBase) and the acts drawn under it
/// (NsAction, NsActionToolbar). The cascade's two names are written here once for the whole
/// chain, which is what keeps a caller — a kit above all — from writing either of them
/// (ui/hosts.md).
///
/// A field reads the same word for itself (NsFieldBase) and a picker reads it for what it
/// decides on top of a field (NsPickerBase); those two chains meet this one only at
/// ComponentBase, so they stay their own (intentional-ui-cases.md).</summary>
public abstract class NsActBase : NsComponent
{
    // Readable by the chain rather than private, for the one component whose body the vendor
    // renders outside this tree: a menu's rows are the popover provider's children, so nothing
    // a form cascaded reaches them and NsMenu hands the two on unchanged (ui/menus.md).
    [CascadingParameter(Name = "ParentDisabled")]
    protected bool ParentDisabled { get; set; }

    [CascadingParameter(Name = "ParentReadOnly")]
    protected bool ParentReadOnly { get; set; }

    /// <summary>Whether the form around this component may be written to at all — false inside
    /// an NsForm that is ReadOnly or Disabled, the latter including the freeze a running submit
    /// puts on every field under it. Read-only and disabled are ONE answer here: what is drawn
    /// is a way in and not a value, so there is nothing to grey out and what both states mean
    /// is that there is no way in.</summary>
    protected bool ParentWritable => !ParentReadOnly && !ParentDisabled;

    /// <summary>Whether this act may be drawn where it stands: an act is a way IN unless it
    /// declares it changes nothing the form holds (ActionItem.Writes), so a form that refuses
    /// writes withholds it. An act nobody classified is taken away rather than left reachable —
    /// erring the other way leaves a write live on a form that refuses writes.</summary>
    protected bool Offers(ActionItem? item)
    {
        return Offers(item?.Writes ?? true);
    }

    /// <summary>The same answer for a way in that carries no item of its own to declare with —
    /// a menu, whose face is what the form takes away (NsMenu.Writes).</summary>
    protected bool Offers(bool writes)
    {
        return ParentWritable || !writes;
    }
}
