// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Components.Tests.Fixtures;

/// <summary>One field carrying both a hint and a DataAnnotations [Required] — the shape that
/// proves the two never occupy the under-field zone at once.</summary>
public sealed class FieldHelperModel
{
    [Required]
    public string? Name { get; set; }
}
