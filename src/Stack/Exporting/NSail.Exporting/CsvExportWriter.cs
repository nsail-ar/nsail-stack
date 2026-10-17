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

    // Material Symbols csv.
    public Glyph Icon => new("""<g transform="translate(0,24) scale(0.025)"><path d="M288-384h72v-48h-72v-96h72v-48h-72q-20.4 0-34.2 13.8Q240-548.4 240-528v96q0 20.4 13.8 34.2Q267.6-384 288-384Zm120 0h96q10.2 0 17.1-6.9 6.9-6.9 6.9-17.1v-72q0-10-6.9-17t-17.1-7h-48v-24h72v-48h-96q-10.2 0-17.1 6.9-6.9 6.9-6.9 17.1v72q0 10 6.9 17t17.1 7h48v24h-72v48Zm208 0h48l56-192h-50l-30 103-30-103h-50l56 192ZM168-192q-29.7 0-50.85-21.16Q96-234.32 96-264.04v-432.24Q96-726 117.15-747T168-768h624q29.7 0 50.85 21.16Q864-725.68 864-695.96v432.24Q864-234 842.85-213T792-192H168Zm0-72h624v-432H168v432Zm0 0v-432 432Z"/></g>""");

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
