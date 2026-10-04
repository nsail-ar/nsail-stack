// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Shapes the OT create form's own two required-field families: a string carrying
/// a DataAnnotations [Required] (DisplayName's family) and a Guid PolicyField whose "unset"
/// sentinel is Guid.Empty (PatientId's family) — RequiredAttribute only rejects null, and
/// Guid.Empty boxes to a non-null value, so this half has no DataAnnotations path at all.
/// CategoryId is that second family reached through the other control: the sentinel behind an
/// autocomplete rather than a select (Nuevo Comprobante's Tipo and Condición fiscal, nsail#1868).</summary>
public sealed class RequiredFieldsModel
{
    [Required]
    public string? Name { get; set; }

    public Guid PatientId { get; set; }

    public Guid CategoryId { get; set; }
}
