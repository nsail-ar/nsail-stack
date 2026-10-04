// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The bindings a field's required mark has to tell apart: a member that declares
/// [Required], one that declares nothing, one whose "empty" is a sentinel no RequiredAttribute
/// can see, a non-nullable value type whose [Required] can never fail, and a member of a row
/// model the form's validator never visits.</summary>
public sealed class RequiredDerivationModel
{
    [Required]
    public string? Declared { get; set; }

    public string? Plain { get; set; }

    public Guid Sentinel { get; set; }

    [Required]
    public int Counted { get; set; }

    [Required]
    public int Enforced { get; set; }

    public bool Conditional { get; set; }

    public string? Conditioned { get; set; }

    public RequiredDerivationLine Line { get; set; } = new();
}

public sealed class RequiredDerivationLine
{
    [Required]
    public string? Description { get; set; }
}
