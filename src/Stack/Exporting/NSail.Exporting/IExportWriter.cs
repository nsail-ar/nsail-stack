// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

using NSail.Icons;

namespace NSail.Exporting;

/// <summary>Turns an <see cref="ExportBook"/> into one file. A writer knows nothing about what it
/// is writing — no screen, no message, no row type — and whatever built the book knows nothing
/// about the file, which is the whole point of there being a book between them.
/// <para>The culture is handed in rather than read off the thread: the file is for the person who
/// asked for it, and on a server that person is a request and never the process. What each writer
/// does with it differs — a spreadsheet stores invariant values and a region only picks the date
/// PATTERN, while a text format has to print every figure in the reader's own separators.</para>
/// <para><see cref="WriteAsync"/> is handed the destination and must stream into it: a book's rows
/// may be an open list of tens of thousands and may be enumerated only once
/// (<see cref="ExportTable.Rows"/>).</para></summary>
public interface IExportWriter
{
    ExportKind Kind { get; }

    /// <summary>The format's own glyph on the menu's option — a writer a host adds brings it,
    /// so the icon answers for the kind and chrome never names one.</summary>
    Glyph Icon { get; }

    /// <summary>Without the dot.</summary>
    string Extension { get; }

    string ContentType { get; }

    Task WriteAsync(ExportBook book, Stream destination, CultureInfo culture, CancellationToken cancellationToken);
}
