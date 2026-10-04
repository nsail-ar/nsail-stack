// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Text;

/// <summary>What of the grammar a caller admits beyond the closed subset every reader shares.
/// A wire whose text is read as text — a WhatsApp body, a mail's text part — takes Prose, where
/// an image and a pipe table are the literal characters their author typed; a document rendered
/// as a page takes Document, where they are an image and a table.</summary>
public sealed record MarkdownOptions
{
    /// <summary>Headings, blockquote, lists, paragraphs, emphasis and a link, and nothing
    /// else — the grammar every channel on the send path reads.</summary>
    public static MarkdownOptions Prose { get; } = new();

    /// <summary>Prose plus images, tables and an id on every heading: a guide chapter, a page
    /// of content.</summary>
    public static MarkdownOptions Document { get; } = new() { Images = true, Tables = true, Anchors = true, AppLinks = true };

    public bool Images { get; init; }

    /// <summary>Whether an address rooted at the app — "/guide/accounting" — or a bare
    /// fragment naming a section of the document being drawn — "#el-qr" — is a link. A
    /// document drawn inside the app knows what "/" and "#" both name; text that leaves over a
    /// wire knows neither — a fragment has no document of its own to land in — so there such an
    /// address stays the literal text its author typed rather than becoming an anchor no mail
    /// client can follow.</summary>
    public bool AppLinks { get; init; }

    public bool Tables { get; init; }

    /// <summary>Whether a heading carries the id its slug names — what an anchor lands on. A
    /// document has places to link to; a message or a step's rendered line has none, and neither
    /// pays for an attribute nothing addresses.</summary>
    public bool Anchors { get; init; }

    /// <summary>Folder a relative image address is resolved against — the chapter's own folder,
    /// so a file names its picture beside it and never the address it is served at. An image
    /// carrying its own scheme ignores it.</summary>
    public string? BasePath { get; init; }
}
