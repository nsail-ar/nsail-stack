// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Icons;

namespace NSail.Types.Tests;

public class SvgSanitizerTests
{
    [Fact]
    public void OnloadHandler_IsDropped()
    {
        var clean = SvgSanitizer.Sanitize("<svg onload=\"x()\"><path d=\"M0 0\"/></svg>");

        Assert.DoesNotContain("onload", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<path", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void UnquotedHandler_IsRefusedAsMalformed()
    {
        Assert.Equal(string.Empty, SvgSanitizer.Sanitize("<svg onload=x>"));
        Assert.False(SvgSanitizer.IsClean("<svg onload=x>"));
    }

    [Fact]
    public void ImageWithErrorHandler_IsDropped()
    {
        var markup = "<image href=\"x\" onerror=\"y\"/>";

        Assert.DoesNotContain("image", SvgSanitizer.Sanitize(markup), StringComparison.OrdinalIgnoreCase);
        Assert.False(SvgSanitizer.IsClean(markup));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<foreignObject><div/></foreignObject>")]
    [InlineData("<a href=\"javascript:alert(1)\"><path d=\"M0 0\"/></a>")]
    [InlineData("<use href=\"javascript:alert(1)\"/>")]
    [InlineData("<use href=\"https://evil/x.svg#a\"/>")]
    [InlineData("<path d=\"M0 0\" style=\"fill:url(https://evil)\"/>")]
    [InlineData("<path d=\"M0 0\" fill=\"url(https://evil/x)\"/>")]
    [InlineData("<style>*{background:url(x)}</style>")]
    [InlineData("<animate attributeName=\"href\" to=\"javascript:alert(1)\"/>")]
    public void Hostile_LeavesNothingExecutable(string markup)
    {
        var clean = SvgSanitizer.Sanitize(markup);

        Assert.False(SvgSanitizer.IsClean(markup));
        Assert.DoesNotContain("javascript", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlainPaths_AreKept()
    {
        var markup = "<g transform=\"translate(0,24) scale(0.025)\"><path d=\"M444-444H240\"/></g>";

        Assert.True(SvgSanitizer.IsClean(markup));
        Assert.Contains("transform=\"translate(0,24) scale(0.025)\"", SvgSanitizer.Sanitize(markup), StringComparison.Ordinal);
        Assert.Contains("d=\"M444-444H240\"", SvgSanitizer.Sanitize(markup), StringComparison.Ordinal);
    }

    [Fact]
    public void LogoDocument_KeepsLocalGradientReferences()
    {
        var markup = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><defs><linearGradient id=\"g\"><stop offset=\"0\" stop-color=\"red\"/></linearGradient></defs><rect width=\"10\" height=\"10\" fill=\"url(#g)\"/></svg>";

        Assert.True(SvgSanitizer.IsClean(markup));
        Assert.Contains("fill=\"url(#g)\"", SvgSanitizer.Sanitize(markup), StringComparison.Ordinal);
    }

    [Fact]
    public void Glyph_IsSanitizedWhenBuiltAndWhenRead()
    {
        var built = new Glyph("<path d=\"M0 0\" onclick=\"x()\"/>");
        var read = JsonSerializer.Deserialize<Glyph>("\"<image href=\\\"x\\\" onerror=\\\"y\\\"/>\"");

        Assert.DoesNotContain("onclick", built.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("image", read.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
