// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// Where one block sits across the width of its column, and how many blocks share that width
// with it. Count 0 or 1 is the whole column, which is the default a block gets for free: a
// stretch of time nothing collides with is never narrowed to leave room for a neighbour that
// does not exist.
public readonly record struct NsTimeLane(int Index, int Count);

// One stretch of time going into the layout, on the same wall clock the grid counts in.
// Priority is the caller's own precedence, and the only thing the layout knows about meaning:
// among blocks that share a cluster, the ones that carry it are placed first, so they take the
// leading lanes. Whose time is more important is the consumer's call, never this file's.
public readonly record struct NsTimeLaneItem(DateTime Starts, TimeSpan Duration, bool Priority);

/// <summary>Splits a column between the blocks that collide inside it — the layout every mature
/// calendar draws. Blocks that touch in time, transitively, form a cluster; the cluster decides
/// how many lanes exist and each of its blocks takes one, so a block alone in its hours keeps
/// the whole column and only the ones that would cover each other pay for it.</summary>
public static class NsTimeLanes
{
    /// <summary>Lanes for the items, index for index. Time is the only input: a block is
    /// narrowed by what overlaps it, never by what kind of thing it is.</summary>
    public static IReadOnlyList<NsTimeLane> Assign(IReadOnlyList<NsTimeLaneItem> items)
    {
        var lanes = new NsTimeLane[items.Count];

        foreach (var cluster in Clusters(items))
        {
            Fill(items, cluster, lanes);
        }

        return lanes;
    }

    // Half-open intervals, so a block ending at eleven and one starting at eleven are two
    // clusters and each keeps the column: they are drawn one below the other and neither
    // hides a pixel of the other.
    static List<List<int>> Clusters(IReadOnlyList<NsTimeLaneItem> items)
    {
        var clusters = new List<List<int>>();
        var current = new List<int>();
        var reach = DateTime.MinValue;

        foreach (var index in Enumerable.Range(0, items.Count).OrderBy(index => items[index].Starts))
        {
            var item = items[index];

            if (current.Count > 0 && item.Starts >= reach)
            {
                clusters.Add(current);
                current = [];
            }

            var end = End(item);

            if (current.Count == 0 || end > reach)
            {
                reach = end;
            }

            current.Add(index);
        }

        if (current.Count > 0)
        {
            clusters.Add(current);
        }

        return clusters;
    }

    // The classic greedy pass: each block takes the first lane whose last block has already
    // ended, and a block that fits nowhere opens one. Priority before start time is the one
    // departure from the textbook order — it is what puts the consumer's own time on the
    // leading edge when two blocks begin at the same minute.
    static void Fill(IReadOnlyList<NsTimeLaneItem> items, List<int> cluster, NsTimeLane[] lanes)
    {
        var ends = new List<DateTime>();
        var placed = new List<(int Item, int Lane)>();

        var order = cluster
            .OrderBy(index => items[index].Priority ? 0 : 1)
            .ThenBy(index => items[index].Starts)
            .ThenBy(index => index);

        foreach (var index in order)
        {
            var item = items[index];
            var lane = ends.FindIndex(end => end <= item.Starts);

            if (lane < 0)
            {
                ends.Add(End(item));
                lane = ends.Count - 1;
            }
            else if (End(item) > ends[lane])
            {
                ends[lane] = End(item);
            }

            placed.Add((index, lane));
        }

        foreach (var (index, lane) in placed)
        {
            lanes[index] = new NsTimeLane(lane, ends.Count);
        }
    }

    // A block with no duration occupies no time at all, so it collides with nothing and is not
    // allowed to drag anything into a cluster with it.
    static DateTime End(NsTimeLaneItem item)
    {
        return item.Duration > TimeSpan.Zero ? item.Starts + item.Duration : item.Starts;
    }
}
