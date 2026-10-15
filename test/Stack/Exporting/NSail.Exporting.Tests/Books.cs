// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Exporting.Tests;

static class Books
{
    public static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly CultureInfo States = CultureInfo.GetCultureInfo("en-US");

    public static readonly IReadOnlyList<ExportColumn> Columns =
    [
        new ExportColumn { Key = "Name", Title = "Nombre" },
        new ExportColumn { Key = "Count", Title = "Cantidad", Format = ExportFormat.Number },
        new ExportColumn { Key = "Total", Title = "Total", Format = ExportFormat.Money },
        new ExportColumn { Key = "On", Title = "Fecha", Format = ExportFormat.Date },
        new ExportColumn { Key = "Share", Title = "Parte", Format = ExportFormat.Percent },
    ];

    public static readonly object?[][] Values =
    [
        ["Ñandú; S.A.", 3, 1234.56m, new DateOnly(2026, 3, 4), 0.125m],
        ["Say \"hello\"", 0, -7m, new DateTime(2026, 12, 31, 18, 45, 0), 1m],
        [null, null, null, null, null],
    ];

    public static ExportBook One(string name = "Lista")
    {
        return ExportBook.Of(name, new ExportTable { Columns = Columns, Rows = Rows(Values) });
    }

    public static async IAsyncEnumerable<ExportRow> Rows(IEnumerable<object?[]> values)
    {
        foreach (var row in values)
        {
            yield return ExportRow.Of(row);
            await Task.Yield();
        }
    }

    public static async Task<byte[]> Write(IExportWriter writer, ExportBook book, CultureInfo culture)
    {
        using var stream = new MemoryStream();

        await writer.WriteAsync(book, stream, culture, CancellationToken.None);

        return stream.ToArray();
    }

    /// <summary>A reader of what the writer wrote, quotes honoured — a naive split on the
    /// separator would find six fields in a row whose name holds one, which is the very case the
    /// quoting exists for.</summary>
    public static string[] Fields(string line, char separator)
    {
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (quoted && character == '"' && index + 1 < line.Length && line[index + 1] == '"')
            {
                field.Append('"');
                index++;
            }
            else if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == separator && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString());

        return [.. fields];
    }
}
