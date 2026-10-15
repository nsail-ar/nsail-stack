// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Exporting.Excel;

namespace NSail.Exporting.Tests;

/// <summary>The promise the model's IAsyncEnumerable makes: a list of tens of thousands of rows
/// exports without the result set ever being in memory, and the rows are asked for ONCE — a
/// second pass would ask a server for every page twice.
/// <para>Nothing here is timed and nothing is compared against a byte count written down: both
/// would be measuring this machine (judgment.md 3). "Bounded" is a claim about GROWTH, so the
/// proof is three exports of the same rows at ten times the length each, with the managed heap
/// SAMPLED while they run — a writer that buffers the sheet grows with the list and a writer that
/// streams does not. The rows themselves are proved gone by weak reference, which needs no
/// figure at all.</para></summary>
public class StreamingTests
{
    const int Rows = 50_000;

    public static TheoryData<ExportKind> Kinds => new(ExportKind.Excel, ExportKind.Csv);

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_fifty_thousand_row_export_does_not_grow_with_the_list(ExportKind kind)
    {
        var writer = Writer(kind);

        var brief = await Peak(writer, Rows / 10);
        var whole = await Peak(writer, Rows);
        var result = Materialized();

        // Ten times the rows, and the slack allowed is a QUARTER of what the same rows cost held
        // — measured on this machine, in this run, which is what keeps the bound off a number
        // written down (judgment.md 3). A writer that held the result set would be four times
        // over the line; the earlier probe of the one that does (MiniExcel's FastMode) was.
        Assert.True(
            whole < brief + result / 4,
            $"{kind} peaked at {whole} bytes for {Rows} rows against {brief} for {Rows / 10}, with the result set itself costing {result}");
    }

    /// <summary>What the rows cost held, which is the figure the export is measured against —
    /// a bound stated as a fraction of it says nothing unless it is the larger number.</summary>
    [Fact]
    public void Held_rows_are_what_this_is_measured_against()
    {
        Assert.True(Materialized() > 2 * 1024 * 1024, $"the same rows held cost only {Materialized()} bytes, so there is nothing for an export to avoid");
    }

    static long Materialized()
    {
        var floor = Floor();
        var held = new List<ExportRow>(Rows);
        var source = new CountingRows(Rows);

        foreach (var row in source.Read().ToBlockingEnumerable())
        {
            held.Add(row);
        }

        var cost = GC.GetTotalMemory(forceFullCollection: true) - floor;

        Assert.Equal(Rows, held.Count);

        return cost;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task No_row_outlives_its_turn(ExportKind kind)
    {
        var source = new CountingRows(Rows, records: true);

        await Export(Writer(kind), source);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal(Rows, source.Produced);
        Assert.Equal(1, source.Passes);

        // The rows are held nowhere but weakly, by the source itself, so whatever is still alive
        // is what the writer kept. One row in hand is expected; the result set is not.
        var alive = source.Handed.Count(row => row.IsAlive);

        Assert.True(alive < 10, $"{kind} still holds {alive} of {Rows} rows after the export");
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task The_rows_arriving_are_the_rows_written(ExportKind kind)
    {
        var source = new CountingRows(1_000);

        using var stream = new MemoryStream();

        await Writer(kind).WriteAsync(Book(source.Read()), stream, Books.Argentina, CancellationToken.None);

        Assert.Equal(1_000, source.Produced);
        Assert.True(stream.Length > 0);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_reader_who_gave_up_stops_the_rows_arriving(ExportKind kind)
    {
        var source = new CountingRows(Rows);

        using var cancellation = new CancellationTokenSource();
        using var stream = new MemoryStream();

        // Cancelled before the first row rather than after a wait: what is being proved is that
        // the writer asks the token, and a deadline would be this machine's clock again.
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Writer(kind).WriteAsync(Book(source.Read()), stream, Books.Argentina, cancellation.Token));

        Assert.True(source.Produced <= 1, $"{source.Produced} rows were asked for after the reader gave up");
    }

    // Sampled rather than read once at the end: what a buffering writer costs is live WHILE it
    // writes and collectable the moment it returns, so a measurement taken after the fact cannot
    // tell the two writers apart.
    //
    // Each sample forces a full collection, which is the whole instrument: an un-forced reading
    // is dominated by the garbage a per-row writer makes between two collections, and that figure
    // grows with the list for a writer that holds nothing at all. What is wanted is what is
    // LIVE, and the only way to ask that is to collect first.
    static async Task<long> Peak(IExportWriter writer, int count)
    {
        var floor = Floor();
        var peak = 0L;
        var running = true;

        var watcher = Task.Run(async () =>
        {
            while (Volatile.Read(ref running))
            {
                peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: true));

                await Task.Delay(10);
            }
        });

        // The source records nothing here: fifty thousand weak references are themselves a list
        // that grows with the list, and they would be measured as the writer's.
        await Export(writer, new CountingRows(count, records: false));

        Volatile.Write(ref running, false);

        await watcher;

        return Math.Max(0, peak - floor);
    }

    static long Floor()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return GC.GetTotalMemory(forceFullCollection: true);
    }

    static async Task Export(IExportWriter writer, CountingRows source)
    {
        await using var sink = Stream.Null;

        await writer.WriteAsync(Book(source.Read()), sink, Books.Argentina, CancellationToken.None);
    }

    static IExportWriter Writer(ExportKind kind)
    {
        return kind == ExportKind.Excel ? new ExcelExportWriter() : new CsvExportWriter();
    }

    static ExportBook Book(IAsyncEnumerable<ExportRow> rows)
    {
        return ExportBook.Of("Lista", new ExportTable { Columns = Books.Columns, Rows = rows });
    }

    sealed class CountingRows(int count, bool records = false)
    {
        public int Produced { get; private set; }

        public int Passes { get; private set; }

        public List<WeakReference> Handed { get; } = records ? new(count) : [];

        public async IAsyncEnumerable<ExportRow> Read()
        {
            Passes++;

            for (var index = 0; index < count; index++)
            {
                Produced++;

                var row = ExportRow.Of(
                    string.Create(CultureInfo.InvariantCulture, $"Row {index} of a list nobody wants to hold"),
                    index,
                    12.34m + index,
                    new DateOnly(2026, 1, 1).AddDays(index % 300),
                    0.1m);

                if (records)
                {
                    Handed.Add(new WeakReference(row));
                }

                yield return row;

                if (index % 1_000 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }
}
