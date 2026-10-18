// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>One family of <see cref="Typefaces"/>' catalog: the stored <see cref="Key"/> a
/// <see cref="Brand"/> carries, the name a picker shows, and the CSS font stack a renderer
/// sets on the brand name and the titles. Never the body, a table, a field or a figure.</summary>
public sealed record Typeface(string Key, string Name, TypefaceCategory Category, string[] FontFamily);

/// <summary>A real choice across families, not a wall of names: the picker groups by this.</summary>
public enum TypefaceCategory
{
    Sans,
    Serif,
    Slab,
    Rounded,
    Display,
}
