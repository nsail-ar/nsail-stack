// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Exporting;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A writer that keeps the book instead of writing a file: what the grid builds is what
/// these tests are about, and what a real writer does with it is NSail.Exporting.Tests'.</summary>
public sealed class RecordingExportWriter(ExportKind kind) : IExportWriter
{
    public ExportKind Kind { get; } = kind;

    public string Extension => Kind == ExportKind.Excel ? "xlsx" : "csv";

    public string ContentType => "application/octet-stream";

    public ExportBook? Book { get; private set; }

    public CultureInfo? Culture { get; private set; }

    public List<ExportRow> Rows { get; } = [];

    public async Task WriteAsync(ExportBook book, Stream destination, CultureInfo culture, CancellationToken cancellationToken)
    {
        Book = book;
        Culture = culture;

        await foreach (var row in book.Pages[0].Tables[0].Rows.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Rows.Add(row);
        }

        destination.WriteByte(1);
    }
}
