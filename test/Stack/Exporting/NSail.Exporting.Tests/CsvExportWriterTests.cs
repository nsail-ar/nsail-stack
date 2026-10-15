// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.Exporting.Tests;

public class CsvExportWriterTests
{
    [Fact]
    public async Task The_file_opens_in_Excel_because_it_carries_a_byte_order_mark()
    {
        var bytes = await Books.Write(new CsvExportWriter(), Books.One(), Books.Argentina);

        Assert.Equal<byte[]>([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("Ñandú", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void The_field_separator_follows_the_decimal_mark()
    {
        Assert.Equal(';', CsvExportWriter.SeparatorFor(Books.Argentina));
        Assert.Equal(',', CsvExportWriter.SeparatorFor(Books.States));
    }

    [Fact]
    public async Task Argentina_reads_a_semicolon_and_a_decimal_comma()
    {
        var lines = await Lines(Books.Argentina);

        Assert.Equal("Nombre;Cantidad;Total;Fecha;Parte", lines[0]);
        Assert.Equal("\"Ñandú; S.A.\";3;1234,56;4/3/2026;12,50%", lines[1]);
    }

    [Fact]
    public async Task English_reads_a_comma_and_a_decimal_point()
    {
        var lines = await Lines(Books.States);

        Assert.Equal("Nombre,Cantidad,Total,Fecha,Parte", lines[0]);
        Assert.Equal("Ñandú; S.A.,3,1234.56,3/4/2026,12.50%", lines[1]);
    }

    [Fact]
    public async Task A_quote_is_doubled_and_a_time_is_kept()
    {
        var lines = await Lines(Books.Argentina);
        var cells = Books.Fields(lines[2], ';');

        Assert.Contains("\"Say \"\"hello\"\"\"", lines[2], StringComparison.Ordinal);
        Assert.Equal("Say \"hello\"", cells[0]);
        Assert.Equal("-7,00", cells[2]);
        Assert.Equal("100,00%", cells[4]);

        // The reader's own short time, whatever the region says it is — a date column holding a
        // timestamp says the hour rather than dropping it.
        Assert.StartsWith("31/12/2026 ", cells[3], StringComparison.Ordinal);
        Assert.Contains(new DateTime(2026, 12, 31, 18, 45, 0).ToString(Books.Argentina.DateTimeFormat.ShortTimePattern, Books.Argentina), cells[3], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_date_with_no_time_says_no_time()
    {
        var lines = await Lines(Books.Argentina);

        Assert.Equal("4/3/2026", Books.Fields(lines[1], ';')[3]);
    }

    [Fact]
    public async Task Nothing_in_a_row_is_nothing_in_the_file()
    {
        var lines = await Lines(Books.Argentina);

        Assert.Equal(";;;;", lines[3]);
    }

    [Fact]
    public async Task The_destination_stays_open_for_whoever_owns_it()
    {
        using var stream = new MemoryStream();

        await new CsvExportWriter().WriteAsync(Books.One(), stream, Books.Argentina, CancellationToken.None);

        Assert.True(stream.CanWrite);
    }

    static async Task<string[]> Lines(System.Globalization.CultureInfo culture)
    {
        var text = Encoding.UTF8.GetString(await Books.Write(new CsvExportWriter(), Books.One(), culture));

        return [.. text.TrimStart('﻿').TrimEnd('\r', '\n').Split('\n').Select(line => line.TrimEnd('\r'))];
    }
}
