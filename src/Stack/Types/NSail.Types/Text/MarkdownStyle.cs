// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Text;

/// <summary>How a markdown block reads once it is no longer HTML: the delimiters emphasis
/// keeps, the marker a bullet takes, and whether a link's address is written out beside its
/// text. Plain is the Stack's own preset; a channel whose wire has its own emphasis
/// characters declares its preset in the kit that owns that channel, so no vendor's
/// vocabulary reaches this project.</summary>
public sealed record MarkdownStyle
{
    /// <summary>Nothing survives but the words: the shape a mail's text part carries.</summary>
    public static MarkdownStyle Plain { get; } = new();

    public string Bold { get; init; } = string.Empty;

    public string Italic { get; init; } = string.Empty;

    public string Bullet { get; init; } = "- ";

    public string Quote { get; init; } = "> ";

    /// <summary>Written as "text (address)" when true; a bare text when the wire makes the
    /// address reachable on its own.</summary>
    public bool ShowsLinkAddress { get; init; } = true;
}
