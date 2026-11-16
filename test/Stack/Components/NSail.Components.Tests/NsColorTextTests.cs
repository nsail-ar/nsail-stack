// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests;

// What a message refuses and what a style paints are one rule: a text [Color] lets through that
// NsColorText would not paint, or the reverse, is a stored value one end shows and the other drops.
public sealed class NsColorTextTests
{
    [Theory]
    [InlineData("#1976d2")]
    [InlineData("rgb(25, 118, 210)")]
    [InlineData("rebeccapurple")]
    [InlineData("red;background:url(//x)")]
    [InlineData("url(//x)")]
    [InlineData("#12'")]
    [InlineData("var(--x)")]
    public void ColorAttribute_AndNsColorText_AcceptTheSameTexts(string text)
    {
        Assert.Equal(NsColorText.IsColor(text), new ColorAttribute().IsValid(text));
    }
}
