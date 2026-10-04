// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

/// <summary>Base for a kit's own picker — the `*Lookup` and `*Select` controls that stand
/// where a field stands without being one. It carries the half of a field's contract that is
/// not about the value: whether the form around it may be written to at all.
///
/// A picker composes a real field and hands its own ReadOnly/Disabled down, and that field
/// reads the form's cascade for itself — so the box draws right either way. What needs this
/// base is everything the PICKER decides on top of the box: a create door, a question a chosen
/// row still owes, a chip's remove. Those read these parameters, and a plain parameter is
/// false on a picker standing inside a read-only form.</summary>
public abstract class NsPickerBase : NsPartial
{
    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [CascadingParameter(Name = "ParentDisabled")]
    bool ParentDisabled { get; set; }

    [CascadingParameter(Name = "ParentReadOnly")]
    bool ParentReadOnly { get; set; }

    /// <summary>What the picker actually is, its own parameter or the form's word — the same
    /// derivation, off the same cascade, that NsFieldBase gives every field. Whatever a picker
    /// gates on writability gates on these two and never on the bare parameters.</summary>
    protected bool IsDisabled => Disabled || ParentDisabled;

    protected bool IsReadOnly => ReadOnly || ParentReadOnly;
}
