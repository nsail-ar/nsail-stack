// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Text;

namespace NSail.Components;

/// <summary>A chapter that arrived: its markdown and the folder it came from, which is what a
/// picture written beside the file resolves against.</summary>
public sealed record GuideChapter(string Body, string Folder)
{
    public MarkdownOptions Options { get; } = MarkdownOptions.Document with { BasePath = Folder };

    public IReadOnlyList<MarkdownHeading> Sections { get; } =
        [.. NSail.Text.Markdown.Headings(Body).Where(heading => heading.Level == SectionLevel)];

    /// <summary>Chapters open at their own title's level, so a section is one step under it —
    /// deeper headings are a section's own structure and stay out of an index that has to read
    /// as one line per entry (ui/wizard.md, the rail is compact by construction).</summary>
    const int SectionLevel = 2;
}
