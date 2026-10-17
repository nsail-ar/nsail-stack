// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using NSail.Exporting;
using NSail.Icons;

namespace NSail.Exporting.Excel;

/// <summary>The book as .xlsx, with typed cells: a figure is a number and a date a date in the
/// sheet, carrying a number format rather than printed text, so the spreadsheet can total a
/// column and sort a date the way the person who opened it expects.
/// <para>One sheet per table — a page with a single table, which is what a list builds, is one
/// sheet named after the page. Values are stored INVARIANT and the reader's region only picks the
/// date pattern: a spreadsheet renders a number in the viewer's own separators, so writing them
/// in the author's would be the figure spelled twice and the cell would arrive as text.</para>
/// </summary>
public sealed class ExcelExportWriter : IExportWriter
{
    public ExportKind Kind => ExportKind.Excel;

    // Material Symbols table_chart.
    public Glyph Icon => new("""<g transform="translate(0,24) scale(0.025)"><path d="M744-144H216q-29 0-50.5-21.5T144-216v-528q0-29.7 21.5-50.85Q187-816 216-816h528q29 0 50.5 21.15T816-744v528q0 29-21.5 50.5T744-144ZM216-636h528v-108H216v108Zm96 72h-96v348h96v-348Zm336 0v348h96v-348h-96Zm-72 0H384v348h192v-348Z"/></g>""");

    public string Extension => "xlsx";

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task WriteAsync(ExportBook book, Stream destination, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(culture);

        var sheets = new Dictionary<string, object>();
        var columns = new List<DynamicExcelColumn>();
        var sheet = 0;

        foreach (var page in book.Pages)
        {
            foreach (var table in page.Tables)
            {
                var keys = new string[table.Columns.Count];

                for (var index = 0; index < table.Columns.Count; index++)
                {
                    var column = table.Columns[index];

                    // The key the row dictionaries carry is the SHEET's, not the column's: the
                    // vendor matches one set of column declarations against every sheet by key,
                    // so two tables both holding a Fecha would share whichever format was
                    // declared first. The reader never sees it — Name is the heading.
                    keys[index] = $"{sheet}.{index}.{column.Key}";

                    columns.Add(new DynamicExcelColumn(keys[index])
                    {
                        Index = index,
                        Name = column.Title,
                        Format = PatternFor(column.Format, culture)
                    });
                }

                sheets[SheetName(table, page, sheets.Keys)] = Rows(table, keys, cancellationToken);
                sheet++;
            }
        }

        // No Culture on the configuration: it makes the vendor PRINT every value in that culture
        // and the cell then arrives as a string, which is the one thing this writer exists to
        // avoid.
        //
        // And no FastMode, despite the name: it buffers the sheet to seek a dimension back into
        // it, so what it costs grows with the list — 104 MiB at 200 000 rows against a flat 10
        // either side of this setting (StreamingTests).
        var configuration = new OpenXmlConfiguration
        {
            DynamicColumns = [.. columns],
            AutoFilter = true,
            FreezeRowCount = 1
        };

        await MiniExcel.SaveAsAsync(destination, sheets, printHeader: true, excelType: ExcelType.XLSX, configuration: configuration, cancellationToken: cancellationToken);
    }

    static async IAsyncEnumerable<IDictionary<string, object?>> Rows(
        ExportTable table,
        string[] keys,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in table.Rows.WithCancellation(cancellationToken))
        {
            // Asked of the token and not left to WithCancellation: a row source is an ordinary
            // async iterator unless it opted into the cancellation parameter, and most do not —
            // a reader who navigated away would otherwise keep a server paging a list nobody is
            // going to read.
            cancellationToken.ThrowIfCancellationRequested();

            var cells = new Dictionary<string, object?>(keys.Length);

            for (var index = 0; index < keys.Length; index++)
            {
                cells[keys[index]] = Cell(index < row.Values.Count ? row.Values[index] : null);
            }

            yield return cells;
        }
    }

    // A date the sheet can hold is a DateTime: the serial number under the cell has no other
    // shape, and handing the vendor anything else leaves it to guess between a date and text.
    static object? Cell(object? value)
    {
        return value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset offset => offset.LocalDateTime,
            TimeOnly time => time.ToTimeSpan(),
            Enum word => word.ToString(),
            _ => value
        };
    }

    // The pattern is the CELL's display, never the value: grouping and the decimal mark are
    // resolved by whoever opens the file, so what is chosen here is only how many decimals the
    // figure is worth and — for a date, which has no universal shape — the reader's own order.
    static string? PatternFor(ExportFormat format, CultureInfo culture)
    {
        return format switch
        {
            ExportFormat.Number => "#,##0.####",
            ExportFormat.Money => "#,##0.00",
            ExportFormat.Percent => "0.00%",
            ExportFormat.Date => culture.DateTimeFormat.ShortDatePattern,
            _ => null
        };
    }

    const int SheetNameLimit = 31;

    // Excel refuses a workbook with two sheets of one name and caps a name at 31 characters,
    // and the characters below are the ones it reserves for a reference to a sheet. A screen's
    // own name reaches here, so none of that can be assumed away. The ordinal is spent only on a
    // name already taken: a book whose tables are named apart keeps the names it was given.
    static string SheetName(ExportTable table, ExportPage page, IEnumerable<string> taken)
    {
        var name = table.Name ?? page.Name;

        foreach (var reserved in @":\/?*[]")
        {
            name = name.Replace(reserved, ' ');
        }

        name = name.Trim();

        if (name.Length == 0)
        {
            name = "1";
        }

        var already = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = Clipped(name, string.Empty);

        for (var ordinal = 2; already.Contains(candidate); ordinal++)
        {
            candidate = Clipped(name, $" ({ordinal})");
        }

        return candidate;
    }

    static string Clipped(string name, string suffix)
    {
        return name.Length + suffix.Length <= SheetNameLimit ? name + suffix : name[..(SheetNameLimit - suffix.Length)] + suffix;
    }
}
