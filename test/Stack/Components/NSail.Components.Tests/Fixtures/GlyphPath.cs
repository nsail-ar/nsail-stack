// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Icons;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A glyph's drawing, so a render can be asked which icon it drew.</summary>
internal static class GlyphPath
{
    internal static string Of(Glyph glyph)
    {
        var match = Regex.Match(glyph.Markup, "d=\"([^\"]+)\"");

        Assert.True(match.Success);

        return match.Groups[1].Value;
    }
}
