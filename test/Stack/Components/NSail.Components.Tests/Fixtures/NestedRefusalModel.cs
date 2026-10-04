// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests.Fixtures;

/// <summary>One nested type held twice: once by a member that opens it to the form
/// ([Validated]) and once by a member that opens nothing. Both render the same fields, so what
/// tells them apart on screen is only the declaration — which is the whole scope question.</summary>
public sealed class NestedRefusalModel
{
    [Validated]
    public NestedRow Opened { get; set; } = new();

    public NestedRow Loose { get; set; } = new();

    [Bounded]
    public decimal? Own { get; set; }
}

public sealed class NestedRow
{
    [Bounded]
    public decimal? Measure { get; set; }

    [Required]
    public string? Note { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class BoundedAttribute : RangeAttribute, ICodedValidation
{
    public BoundedAttribute()
        : base(-30d, 30d)
    {
    }

    public string Code
    {
        get { return "Bounded"; }
    }

    public IReadOnlyDictionary<string, string>? Arguments
    {
        get { return new Dictionary<string, string> { ["from"] = "-30.00", ["to"] = "30.00" }; }
    }
}
