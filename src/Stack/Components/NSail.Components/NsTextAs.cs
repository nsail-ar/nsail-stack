// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Semantic role for NsText — Title picks the heading treatment; add Subtitle/
/// Caption here if they show up, never as their own NsSize token.</summary>
public enum NsTextAs
{
    Body,
    Title,
    Subtitle
}
