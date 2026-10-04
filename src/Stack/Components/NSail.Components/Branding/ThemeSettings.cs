// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Per-user theme; null follows the brand default.</summary>
public sealed class ThemeSettings
{
    public bool? DarkMode { get; set; }
}
