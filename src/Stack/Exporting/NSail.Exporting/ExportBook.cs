// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>What is being exported, whole: the file's own name — without an extension, which is
/// the writer's — and its pages. This is the format-neutral end of the export: everything above
/// it builds one of these from what it is showing, every writer below it turns one into a file,
/// and neither knows the other.</summary>
public sealed record ExportBook
{
    public required string Name { get; init; }

    public required IReadOnlyList<ExportPage> Pages { get; init; }

    /// <summary>The one-page, one-table book a list is: the shape nearly every caller wants, so
    /// no screen builds the two wrappers by hand.</summary>
    public static ExportBook Of(string name, ExportTable table)
    {
        return new ExportBook { Name = name, Pages = [ExportPage.Of(name, table)] };
    }
}
