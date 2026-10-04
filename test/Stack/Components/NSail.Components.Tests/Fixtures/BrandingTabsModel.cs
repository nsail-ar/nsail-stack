// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Shapes BrandingPage's own tab split: Light carries a logo and colors, Dark
/// carries a logo and colors, Print carries only a logo — no color fields at all, which is
/// Leonardo's explicit mandate for the print tab.</summary>
public sealed class BrandingTabsModel
{
    public string? LightPrimary { get; set; }

    [Required]
    public string? DarkPrimary { get; set; }

    public string? PrintLogoAssetId { get; set; }
}
