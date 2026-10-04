// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Text;

/// <summary>A heading a document carries, as an index reads it: how deep it sits, the words it
/// shows once its own markup is spent, and the slug the rendered heading wears as its id — so a
/// table of contents and the anchors it points at are one derivation, never two.</summary>
public sealed record MarkdownHeading(int Level, string Text, string Slug);
