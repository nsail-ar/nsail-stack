// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Text;

namespace NSail.Components;

/// <summary>The renderer's answer as a MarkupString, for a component that draws it. The subset
/// itself lives in NSail.Text so the send path — which must not reference a UI project — reads
/// the same one.</summary>
public static class Markdown
{
    public static MarkupString ToHtml(string? markdown)
    {
        return ToHtml(markdown, null);
    }

    public static MarkupString ToHtml(string? markdown, MarkdownOptions? options)
    {
        return new MarkupString(NSail.Text.Markdown.ToHtml(markdown, options));
    }
}
