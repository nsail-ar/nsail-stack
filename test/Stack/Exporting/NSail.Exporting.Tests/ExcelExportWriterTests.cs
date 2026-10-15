// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using ClosedXML.Excel;
using NSail.Exporting.Excel;

namespace NSail.Exporting.Tests;

/// <summary>Every assertion here is made by a DIFFERENT OpenXML implementation than the one that
/// wrote the file: a workbook ClosedXML opens and reports typed cells for is a workbook Excel
/// opens, which is the only way this suite can speak about the spreadsheet at all.</summary>
public class ExcelExportWriterTests
{
    [Fact]
    public async Task A_number_stays_a_number_and_a_date_stays_a_date()
    {
        var sheet = await Sheet(Books.Argentina);

        Assert.Equal(XLDataType.Text, sheet.Cell(2, 1).Value.Type);
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 2).Value.Type);
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 3).Value.Type);
        Assert.Equal(XLDataType.DateTime, sheet.Cell(2, 4).Value.Type);
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 5).Value.Type);

        Assert.Equal(1234.56, sheet.Cell(2, 3).Value.GetNumber(), 5);
        Assert.Equal(new DateTime(2026, 3, 4), sheet.Cell(2, 4).Value.GetDateTime());
        Assert.Equal(0.125, sheet.Cell(2, 5).Value.GetNumber(), 5);
    }

    [Fact]
    public async Task A_figure_carries_a_number_format_and_never_the_authors_separators()
    {
        var sheet = await Sheet(Books.Argentina);

        Assert.Equal("#,##0.####", sheet.Cell(2, 2).Style.NumberFormat.Format);
        Assert.Equal("#,##0.00", sheet.Cell(2, 3).Style.NumberFormat.Format);
        Assert.Equal("0.00%", sheet.Cell(2, 5).Style.NumberFormat.Format);
    }

    [Fact]
    public async Task Only_the_date_pattern_follows_the_reader()
    {
        Assert.Equal("d/M/yyyy", (await Sheet(Books.Argentina)).Cell(2, 4).Style.NumberFormat.Format);
        Assert.Equal("M/d/yyyy", (await Sheet(Books.States)).Cell(2, 4).Style.NumberFormat.Format);

        // The VALUE is the same figure in both: a spreadsheet renders a number in the separators
        // of whoever opens it, so a region reaching the value would be the figure spelled twice.
        Assert.Equal(1234.56, (await Sheet(Books.Argentina)).Cell(2, 3).Value.GetNumber(), 5);
        Assert.Equal(1234.56, (await Sheet(Books.States)).Cell(2, 3).Value.GetNumber(), 5);
    }

    [Fact]
    public async Task An_empty_row_writes_no_values()
    {
        var sheet = await Sheet(Books.Argentina);

        Assert.All(Enumerable.Range(1, Books.Columns.Count), column => Assert.True(sheet.Cell(4, column).IsEmpty()));
    }

    [Fact]
    public async Task The_headings_are_the_columns_titles_and_the_sheet_is_the_pages_name()
    {
        var sheet = await Sheet(Books.Argentina);

        Assert.Equal("Lista", sheet.Name);
        Assert.Equal(Books.Columns.Select(column => column.Title), Enumerable.Range(1, Books.Columns.Count).Select(column => sheet.Cell(1, column).GetString()));
    }

    [Fact]
    public async Task A_sheet_name_a_screen_produced_is_made_legal()
    {
        using var stream = new MemoryStream(await Books.Write(new ExcelExportWriter(), Books.One("Pedidos 01/03 - 31/03 de la sucursal centro"), Books.Argentina));
        using var workbook = new XLWorkbook(stream);

        Assert.Equal("Pedidos 01 03 - 31 03 de la suc", workbook.Worksheet(1).Name);
        Assert.Equal(31, workbook.Worksheet(1).Name.Length);
    }

    static async Task<IXLWorksheet> Sheet(System.Globalization.CultureInfo culture)
    {
        var stream = new MemoryStream(await Books.Write(new ExcelExportWriter(), Books.One(), culture));

        return new XLWorkbook(stream).Worksheet(1);
    }
}
