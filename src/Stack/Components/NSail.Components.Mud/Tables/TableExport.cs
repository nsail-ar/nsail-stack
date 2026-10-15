// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.CompilerServices;
using NSail.Exporting;
using NSail.Paging;

namespace NSail.Components;

/// <summary>Turns a list's pages into the rows of an <see cref="ExportTable"/>: every page the
/// current filter answers, in order, pulled one at a time and dropped as it is written.
/// <para>A reader rather than the grid's own paging, so the arithmetic — where the list ends, what
/// a page that does not answer means, how a row becomes values — is settled without a renderer.
/// What the grid supplies is one line: ask the screen for a page and hand back what it read.</para>
/// </summary>
public static class TableExport
{
    /// <summary>What the pages of an export are asked for in: the largest any message in the tree
    /// accepts, because the round trips are the cost and no page is held (PagedMessage).</summary>
    public const int PageSize = PagedMessage.MaxPageSize;

    /// <summary>The rows, lazily. <paramref name="read"/> answers a page or null for a read that
    /// did not land — a refusal, or an answer that never reached the caller — and null ends the
    /// export: half a list downloaded as a whole one is worse than no file. The caller tells the
    /// two apart and says so, which is why this throws instead of stopping quietly.</summary>
    public static async IAsyncEnumerable<ExportRow> Rows<TRow>(
        Func<int, int, Task<DataPage<TRow>?>> read,
        IReadOnlyList<TableColumn> columns,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(columns);

        var index = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await read(index, PageSize);

            if (page is null)
            {
                throw new ExportInterrupted();
            }

            foreach (var row in page.Items)
            {
                yield return new ExportRow { Values = [.. columns.Select(column => column.Read(row))] };
            }

            // The count, not the total: a total is a figure the server computed for a pager, and a
            // filter applied between two pages makes it a lie — while a page shorter than the one
            // asked for is the end of the list however the count was arrived at. An empty page is
            // covered by the same line, which is what ends a list whose length divides exactly.
            if (page.Items.Count < PageSize)
            {
                yield break;
            }

            index++;
        }
    }
}

/// <summary>A page of an export did not answer, so there is no file. Carried as an exception
/// because a writer is already streaming by the time it happens and a half-written file must not
/// be offered.</summary>
public sealed class ExportInterrupted : Exception
{
    public ExportInterrupted()
        : base("A page of the export did not answer.")
    {
    }

    public ExportInterrupted(string message)
        : base(message)
    {
    }

    public ExportInterrupted(string message, Exception inner)
        : base(message, inner)
    {
    }
}
