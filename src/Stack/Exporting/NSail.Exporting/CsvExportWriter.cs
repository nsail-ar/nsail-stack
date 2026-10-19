// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text;

using NSail.Icons;

namespace NSail.Exporting;

/// <summary>The book as text, for the spreadsheet of whoever does not have one: UTF-8 with a
/// byte-order mark, because Excel reads a CSV without one as the machine's legacy code page and
/// an accented name arrives broken.
/// <para>CSV has no sheets, so the pages flatten in order, each table's headings above its own
/// rows and a blank line between tables — the file is one reader's document, not a structure to
/// be parsed back.</para></summary>
public sealed class CsvExportWriter : IExportWriter
{
    public ExportKind Kind => ExportKind.Csv;

    public Glyph Icon => ExportingIcons.Csv;

    public string Extension => "csv";

    public string ContentType => "text/csv";

    public async Task WriteAsync(ExportBook book, Stream destination, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(culture);

        var separator = SeparatorFor(culture);

        // LeaveOpen: the destination belongs to the caller — a response body, a stream the
        // browser is already pulling from — and closing it here would end the download at the
        // first writer that finished.
        await using var text = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 16 * 1024, leaveOpen: true);

        var first = true;

        foreach (var page in book.Pages)
        {
            foreach (var table in page.Tables)
            {
                if (!first)
                {
                    await text.WriteLineAsync();
                }

                first = false;

                await text.WriteLineAsync(string.Join(separator, table.Columns.Select(column => Field(column.Title, separator))));

                await foreach (var row in table.Rows.WithCancellation(cancellationToken))
                {
                    // Asked of the token and not left to WithCancellation: a row source is an
                    // ordinary async iterator unless it opted into the cancellation parameter,
                    // and most do not — a reader who navigated away would otherwise keep a
                    // server paging a list nobody is going to read.
                    cancellationToken.ThrowIfCancellationRequested();

                    var cells = table.Columns.Select((column, index) =>
                        Field(ExportText.Of(index < row.Values.Count ? row.Values[index] : null, column.Format, culture), separator));

                    await text.WriteLineAsync(string.Join(separator, cells));
                }
            }
        }

        await text.FlushAsync(cancellationToken);
    }

    /// <summary>Derived from the decimal mark and never configured: a culture that writes
    /// 1.234,56 cannot separate fields with the comma it puts inside its own figures, which is
    /// the whole reason es-AR takes the semicolon and en-US the comma. Reading it off the region
    /// would be the same answer from a value that may be tailored per machine, and a mismatch
    /// between the two is a file that opens as one column.</summary>
    public static char SeparatorFor(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return culture.NumberFormat.NumberDecimalSeparator == "," ? ';' : ',';
    }

    static string Field(string value, char separator)
    {
        if (value.Length == 0)
        {
            return value;
        }

        if (!value.Contains(separator) && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
