// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using ClosedXML.Excel;
using NSail.Exporting.Excel;

namespace NSail.Exporting.Tests;

/// <summary>One book, both writers: the model is format-neutral only if the same tables reach
/// .xlsx and .csv without either writer needing something the other does not.</summary>
public class BothFormatsTests
{
    [Fact]
    public async Task Each_writer_states_the_file_it_produces()
    {
        var writers = new ExportWriters([new CsvExportWriter(), new ExcelExportWriter()]);

        Assert.Equal([ExportKind.Excel, ExportKind.Csv], writers.All.Select(writer => writer.Kind));
        Assert.Equal("xlsx", writers.For(ExportKind.Excel)!.Extension);
        Assert.Equal("csv", writers.For(ExportKind.Csv)!.Extension);
    }

    [Fact]
    public void A_kind_nobody_registered_is_not_offered()
    {
        var writers = new ExportWriters([new CsvExportWriter()]);

        Assert.Null(writers.For(ExportKind.Excel));
        Assert.False(writers.IsEmpty);
        Assert.True(new ExportWriters([]).IsEmpty);
    }

    [Fact]
    public void A_screens_name_becomes_a_file_name()
    {
        var book = Books.One("Pedidos 01/03 - 31/03");

        Assert.Equal("Pedidos 01-03 - 31-03.xlsx", ExportWriters.FileName(book, new ExcelExportWriter()));
        Assert.Equal("Pedidos 01-03 - 31-03.csv", ExportWriters.FileName(book, new CsvExportWriter()));
    }

    [Fact]
    public async Task The_same_table_reaches_both_formats_with_the_same_headings_and_rows()
    {
        var csv = Encoding.UTF8.GetString(await Books.Write(new CsvExportWriter(), Books.One(), Books.Argentina));
        var xlsx = await Books.Write(new ExcelExportWriter(), Books.One(), Books.Argentina);

        using var stream = new MemoryStream(xlsx);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        var lines = csv.TrimEnd('\r', '\n').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

        Assert.Equal(Books.Values.Length + 1, lines.Length);

        foreach (var (column, index) in Books.Columns.Select((column, index) => (column, index)))
        {
            Assert.Equal(column.Title, sheet.Cell(1, index + 1).GetString());
            Assert.Contains(column.Title, lines[0], StringComparison.Ordinal);
        }

        // Both files place every row the book had, and the figures land on the same column — the
        // text format spells them for a reader, the sheet keeps them as values. A row with
        // nothing in it is an empty line either side: a sheet writes no cells for it, which is
        // why the LAST USED row of the two files differs and the row count does not.
        for (var row = 0; row < Books.Values.Length; row++)
        {
            var money = (decimal?)Books.Values[row][2];

            var cells = Books.Fields(lines[row + 1], ';');

            Assert.Equal(Books.Columns.Count, cells.Length);
            Assert.Equal(money?.ToString("0.00", Books.Argentina) ?? string.Empty, cells[2]);
            Assert.Equal(money is null, sheet.Cell(row + 2, 3).IsEmpty());

            if (money is not null)
            {
                Assert.Equal((double)money, sheet.Cell(row + 2, 3).Value.GetNumber(), 5);
            }
        }
    }

    [Fact]
    public async Task A_row_shorter_than_the_columns_leaves_the_rest_empty()
    {
        var table = new ExportTable
        {
            Columns = Books.Columns,
            Rows = Books.Rows([["Only a name"]])
        };

        var csv = Encoding.UTF8.GetString(await Books.Write(new CsvExportWriter(), ExportBook.Of("Lista", table), Books.Argentina));

        Assert.Contains("Only a name;;;;", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_book_of_several_pages_becomes_a_sheet_each_and_one_csv()
    {
        var book = new ExportBook
        {
            Name = "Cierre",
            Pages =
            [
                ExportPage.Of("Ventas", new ExportTable { Columns = Books.Columns, Rows = Books.Rows(Books.Values) }),
                ExportPage.Of("Compras", new ExportTable { Columns = [new ExportColumn { Key = "On", Title = "Fecha", Format = ExportFormat.Date }], Rows = Books.Rows([[new DateOnly(2026, 1, 2)]]) }),
            ]
        };

        using var stream = new MemoryStream(await Books.Write(new ExcelExportWriter(), book, Books.Argentina));
        using var workbook = new XLWorkbook(stream);

        Assert.Equal(["Ventas", "Compras"], workbook.Worksheets.Select(sheet => sheet.Name));

        // The second page's own Fecha keeps its own format: the vendor matches one set of column
        // declarations against every sheet by key, so the writer keys them per sheet.
        Assert.Equal(Books.Argentina.DateTimeFormat.ShortDatePattern, workbook.Worksheet("Compras").Cell(2, 1).Style.NumberFormat.Format);
        Assert.Equal(XLDataType.DateTime, workbook.Worksheet("Compras").Cell(2, 1).Value.Type);
    }

    [Fact]
    public async Task Two_tables_of_one_name_get_two_sheets()
    {
        var table = new ExportTable { Columns = Books.Columns, Rows = Books.Rows(Books.Values) };

        var book = new ExportBook
        {
            Name = "Lista",
            Pages = [ExportPage.Of("Lista", table), ExportPage.Of("Lista", table with { Rows = Books.Rows(Books.Values) })]
        };

        using var stream = new MemoryStream(await Books.Write(new ExcelExportWriter(), book, Books.Argentina));
        using var workbook = new XLWorkbook(stream);

        Assert.Equal(["Lista", "Lista (2)"], workbook.Worksheets.Select(sheet => sheet.Name));
    }
}
