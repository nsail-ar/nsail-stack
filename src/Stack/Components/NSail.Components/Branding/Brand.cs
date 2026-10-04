// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>
/// Product/tenant branding: a simple serializable object translated into the
/// vendor theme by the rendering package (e.g. NSail.Components.Mud).
/// </summary>
public sealed class Brand
{
    public string Name { get; set; } = "NSail";

    public bool DefaultDark { get; set; } = true;

    public BrandTheme Light { get; set; } = BrandTheme.DefaultLight();

    public BrandTheme Dark { get; set; } = BrandTheme.DefaultDark();
}
