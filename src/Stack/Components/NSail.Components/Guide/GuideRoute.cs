// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

/// <summary>Which page an app's guide lives at. The address is the product's — the Stack
/// cannot name a page it does not own — so the app registers its own screen here and every
/// door to the guide resolves through the route table like any other typed URL.</summary>
public sealed class GuideRoute(Type pageType, RouteTable routes)
{
    public Type PageType { get; } = pageType;

    /// <summary>The guide's address at a chapter, and its front door when none is named. The
    /// section is not part of it: an anchor belongs to the document the browser lands on, so it
    /// rides the href rather than the route a surface carries in its query.</summary>
    public string GetUrl(string? chapter)
    {
        return string.IsNullOrWhiteSpace(chapter)
            ? routes.GetUrl(PageType)
            : routes.GetUrl(PageType, new { chapter });
    }

    /// <summary>A topic read as its two halves: "{chapter}", "{chapter}#{section}" or
    /// "#{section}" of the chapter already open.</summary>
    public static (string? Chapter, string? Section) Split(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return (null, null);
        }

        var mark = topic.IndexOf('#', StringComparison.Ordinal);

        if (mark < 0)
        {
            return (topic, null);
        }

        var chapter = mark == 0 ? null : topic[..mark];
        var section = topic[(mark + 1)..];

        return (chapter, string.IsNullOrEmpty(section) ? null : section);
    }
}
