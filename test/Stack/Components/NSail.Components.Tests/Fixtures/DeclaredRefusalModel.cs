// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The three bindings the report names, each an annotation that is false for every
/// prefix of its own answer: the CUIT of Configuración &gt; ARCA, the mailbox of Correo &gt;
/// Probar, and the point of sale whose emptied box is a zero out of range. Name carries the
/// requirement, so the submit has something to refuse while nothing was typed into it, and
/// NamedCuit is the same pattern reached the other way — a box bound by hand, whose For is the
/// expression a shared editor hands the field.</summary>
public sealed class DeclaredRefusalModel
{
    [Required]
    public string? Name { get; set; }

    [RegularExpression("^[0-9]{2}-?[0-9]{8}-?[0-9]$")]
    public string? Cuit { get; set; }

    [EmailAddress]
    public string? MailAddress { get; set; }

    [Range(1, 99999)]
    public int PointOfSale { get; set; }

    [Required]
    [RegularExpression("^[0-9]{2}-?[0-9]{8}-?[0-9]$")]
    public string? NamedCuit { get; set; }
}
