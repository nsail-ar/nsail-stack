// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.Forms;

namespace NSail.Components;

/// <summary>Lets a field announce the identifier it binds to an ancestor that wants to know
/// which fields render inside it — a tab checking whether one of its own fields currently
/// carries a validation message, and the form deciding whether an issue naming a member has
/// a field to draw under at all. A tracker that is itself nested forwards to the one above
/// it, so a field inside a tab is known to the form too, without the field naming either.</summary>
public interface IFieldTracker
{
    void Track(FieldIdentifier identifier);

    /// <summary>The field is going away. Without this a member whose field was removed from
    /// the tree would keep counting as rendered, and its message would be attached to an
    /// input nobody can see — the exact failure the tracking exists to prevent.</summary>
    void Untrack(FieldIdentifier identifier);
}
