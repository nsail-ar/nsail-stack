// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

using ClosedXML.Excel;

using NSail.Exporting.Excel;

namespace NSail.Exporting.Tests;

public class ExportRobustnessTests
{
    static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
    static readonly TimeZoneInfo Lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    static ExportBook Book(ExportFormat format, params object?[] values)
    {
        return new ExportBook
        {
            Name = "Robust",
            Pages = [ExportPage.Of("P", new ExportTable
            {
                Columns = [new ExportColumn { Key = "V", Title = "V", Format = format }],
                Rows = Books.Rows([.. values.Select(value => new object?[] { value })])
            })]
        };
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task A_non_finite_figure_is_an_empty_cell_and_the_export_completes(double value)
    {
        var csv = Encoding.UTF8.GetString(await Books.Write(new CsvExportWriter(), Book(ExportFormat.Number, value, 2.5), Books.States));
        var lines = csv.TrimStart('﻿').TrimEnd('\r', '\n').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

        Assert.Equal(["V", string.Empty, "2.5"], lines);

        using var workbook = new XLWorkbook(new MemoryStream(await Books.Write(new ExcelExportWriter(), Book(ExportFormat.Number, value, 2.5), Books.States)));

        Assert.True(workbook.Worksheet(1).Cell(2, 1).IsEmpty());
        Assert.Equal(2.5, workbook.Worksheet(1).Cell(3, 1).Value.GetNumber());
    }

    [Fact]
    public async Task A_UTC_moment_reads_in_the_zone_it_is_handed()
    {
        var at = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);

        var tokyo = Encoding.UTF8.GetString(await Books.Write(new CsvExportWriter(), Book(ExportFormat.Date, at, at.UtcDateTime), Books.States, Tokyo));
        var lima = Encoding.UTF8.GetString(await Books.Write(new CsvExportWriter(), Book(ExportFormat.Date, at, at.UtcDateTime), Books.States, Lima));

        var pattern = $"{Books.States.DateTimeFormat.ShortDatePattern} {Books.States.DateTimeFormat.ShortTimePattern}";

        Assert.Equal(2, tokyo.Split(new DateTime(2026, 3, 4, 21, 0, 0).ToString(pattern, Books.States)).Length - 1);
        Assert.Equal(2, lima.Split(new DateTime(2026, 3, 4, 7, 0, 0).ToString(pattern, Books.States)).Length - 1);

        using var workbook = new XLWorkbook(new MemoryStream(await Books.Write(new ExcelExportWriter(), Book(ExportFormat.Date, at, at.UtcDateTime), Books.States, Tokyo)));

        Assert.Equal(new DateTime(2026, 3, 4, 21, 0, 0), workbook.Worksheet(1).Cell(2, 1).Value.GetDateTime());
        Assert.Equal(new DateTime(2026, 3, 4, 21, 0, 0), workbook.Worksheet(1).Cell(3, 1).Value.GetDateTime());
    }

    [Fact]
    public void A_file_name_loses_the_same_characters_on_every_OS()
    {
        var book = new ExportBook { Name = "a\"b\\c:d/e\r\nf*g", Pages = [] };

        Assert.Equal("a-b-c-d-e--f-g.csv", ExportWriters.FileName(book, new CsvExportWriter()));
    }
}
