// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;
using NSail.Exporting;
using NSail.Localization;
using NSail.Metadata;
using NSail.Paging;

namespace NSail.Components.Tests;

/// <summary>What an export reaches: every page the list's own filter answers, in order, not the
/// page on screen. Read through a reader rather than through the grid, so where the list ends and
/// what a page that does not answer means are settled with no renderer in the way — the grid's own
/// half of it is one line (NsTable.Export) and a browser is what proves that one
/// (NSail.Optical.E2E).</summary>
public class TableExportPagesTests
{
    const int Size = TableExport.PageSize;

    static readonly IReadOnlyList<TableColumn> Columns = Named(nameof(ExportRowModel.Name), nameof(ExportRowModel.Count), nameof(ExportRowModel.Kind));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(Size - 1)]
    [InlineData(Size)]
    [InlineData(Size + 1)]
    [InlineData(2 * Size)]
    [InlineData((2 * Size) + 350)]
    public async Task Every_row_of_the_register_reaches_the_file(int count)
    {
        var register = new Register(count);

        var rows = await Read(register);

        Assert.Equal(count, rows.Count);
        Assert.Equal(Enumerable.Range(0, count).Select(index => $"Row {index}"), rows.Select(row => row.Values[0]));
    }

    /// <summary>A list whose length divides exactly by the page size still ends: the empty page
    /// after the last full one is the signal, and the row count is what reads it.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(Size, 2)]
    [InlineData(2 * Size, 3)]
    [InlineData((2 * Size) + 1, 3)]
    public async Task The_pages_asked_for_are_the_pages_the_list_has(int count, int expected)
    {
        var register = new Register(count);

        await Read(register);

        Assert.Equal(Enumerable.Range(0, expected).Select(index => (index, Size)), register.Asked);
    }

    /// <summary>A total the server computed for a pager is not what ends the export: a filter
    /// applied between two pages makes it a lie, so what is read is the page in hand.</summary>
    [Fact]
    public async Task A_total_that_lies_does_not_decide_where_the_list_ends()
    {
        var register = new Register(Size + 5) { Total = 9_999_999 };

        var rows = await Read(register);

        Assert.Equal(Size + 5, rows.Count);
        Assert.Equal(2, register.Asked.Count);
    }

    [Fact]
    public async Task A_page_that_does_not_answer_leaves_no_file()
    {
        var register = new Register(3 * Size) { RefuseFrom = 1 };

        await Assert.ThrowsAsync<ExportInterrupted>(() => Read(register));

        Assert.Equal(2, register.Asked.Count);
    }

    [Fact]
    public async Task A_reader_who_gave_up_stops_the_pages()
    {
        var register = new Register(5 * Size);

        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in TableExport.Rows<ExportRowModel>(register.Read, Columns, cancellation.Token))
            {
                await cancellation.CancelAsync();
            }
        });

        Assert.Equal(1, register.Asked.Count);
    }

    /// <summary>The values are the columns', in the columns' order — and an enum reads as the word
    /// the cell shows (TableColumn).</summary>
    [Fact]
    public async Task A_row_becomes_its_columns_values_in_order()
    {
        var rows = await Read(new Register(2));

        Assert.Equal(["Row 0", 0, "Persona"], rows[0].Values);
        Assert.Equal(["Row 1", 1, "Comercio"], rows[1].Values);
    }

    /// <summary>No cell type holds a list, and a column of them is one the screen already joins
    /// by hand (a party's roles). Left alone the file would print the type's name.</summary>
    [Fact]
    public async Task A_collection_column_reads_as_its_members()
    {
        var rows = await Read(new Register(1), Named(nameof(ExportRowModel.Roles)));

        Assert.Equal("Cliente, Proveedor", rows[0].Values[0]);
    }

    /// <summary>The columns are the header's, so a column the header stopped declaring — one a
    /// list's own filter dropped — is not in the file.</summary>
    [Fact]
    public async Task Only_the_declared_columns_are_read()
    {
        var rows = await Read(new Register(1), Named(nameof(ExportRowModel.Total)));

        Assert.Equal([10m], rows[0].Values);
    }

    static async Task<List<ExportRow>> Read(Register register, IReadOnlyList<TableColumn>? columns = null)
    {
        var rows = new List<ExportRow>();

        await foreach (var row in TableExport.Rows<ExportRowModel>(register.Read, columns ?? Columns, CancellationToken.None))
        {
            rows.Add(row);
        }

        return rows;
    }

    // Built the way the header builds them, from the member a For expression named: what the
    // export reads is the declaration and never the row type's other members.
    static IReadOnlyList<TableColumn> Named(params string[] members)
    {
        var metadata = new MetadataProvider();
        var strings = new StringManager(
            new StringCatalog([new FixedStrings(new Dictionary<string, string>
            {
                [metadata.KeyFor(ExportRowKind.Person)] = "Persona",
                [metadata.KeyFor(ExportRowKind.Shop)] = "Comercio",
            })]),
            new LanguageProvider(),
            metadata);

        return
        [
            .. members.Select(member => TableColumn.Of(
                typeof(ExportRowModel),
                member,
                member,
                metadata.KeyFor(typeof(ExportRowModel), member),
                null,
                strings)!)
        ];
    }

    sealed class Register(int count)
    {
        public List<(int Index, int Size)> Asked { get; } = [];

        public int? Total { get; init; }

        public int? RefuseFrom { get; init; }

        public Task<DataPage<ExportRowModel>?> Read(int index, int size)
        {
            Asked.Add((index, size));

            if (RefuseFrom is { } refused && index >= refused)
            {
                return Task.FromResult<DataPage<ExportRowModel>?>(null);
            }

            var start = index * size;
            var taken = Math.Clamp(count - start, 0, size);

            return Task.FromResult<DataPage<ExportRowModel>?>(new DataPage<ExportRowModel>
            {
                Items = [.. Enumerable.Range(start, taken).Select(Make)],
                PageIndex = index,
                PageSize = size,
                TotalCount = Total ?? count
            });
        }

        static ExportRowModel Make(int index)
        {
            return new ExportRowModel
            {
                Name = $"Row {index}",
                Count = index,
                Total = 10m + index,
                Share = 0.25m,
                On = new DateOnly(2026, 1, 1).AddDays(index % 300),
                Kind = index % 2 == 0 ? ExportRowKind.Person : ExportRowKind.Shop,
                Roles = ["Cliente", "Proveedor"]
            };
        }
    }
}
