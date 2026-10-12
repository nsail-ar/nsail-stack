// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The declarations a box has to tell apart: each of the two annotations that refuse a
/// length on the wire, a subclass of one of them, a member declaring none, a bound under a mask
/// — and one declared on a FIGURE, which is there so the box that must ignore it is proved to
/// ignore it rather than merely having nothing to read. Nothing in the suite submits this
/// model: a length over a decimal is what the wire would never carry, and validating it is the
/// cast the BCL attribute cannot make.</summary>
public sealed class DeclaredLengthModel
{
    [MaxLength(30)]
    public string? Number { get; set; }

    [StringLength(12)]
    public string? Code { get; set; }

    [SearchTerm]
    public string? Term { get; set; }

    public string? Free { get; set; }

    [MaxLength(11)]
    public string? Masked { get; set; }

    [MaxLength(4)]
    public decimal? Price { get; set; }

    [Validated]
    public DeclaredLengthRow Opened { get; set; } = new();

    public DeclaredLengthRow Loose { get; set; } = new();
}

/// <summary>One nested type held twice, once by a member that opens it to the form's own
/// validator and once by a member that opens nothing — the scope question, which a bound
/// answers exactly as the required mark does.</summary>
public sealed class DeclaredLengthRow
{
    [MaxLength(8)]
    public string? Note { get; set; }
}
