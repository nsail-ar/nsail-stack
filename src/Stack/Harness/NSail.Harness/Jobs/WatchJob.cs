// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;
using NSail.Harness.Watching;

namespace NSail.Harness.Jobs;

// A Watch worker's pass: the error lines its source logged since the last pass, grouped by
// fingerprint, each group matched to the stories already filed for it (label `telemetry`, the
// fingerprint's marker). A group with an open story adds its count there, at most once a day. One
// with none files a story in Analyze, for the analyst to read the cause; one whose story closed and
// that was logged again after the close files a new story naming the old one: it broke twice. A
// group seen in production also wears `bug`, which goes before everything; one whose last story
// closed as not planned is muted. No agent runs: the cause is the analyst's to read.
public sealed class WatchJob(WorkItem item, JobContext ctx, WatchState state) : IJob
{
    // The most error lines one pass reads; past that the count is a floor.
    const int MaxLines = 5000;

    static readonly string[] NotProdDefault = ["test", "qa", "canary"];

    public WorkItem Item { get; } = item;

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var name = Item.Worker!;
        var w = ctx.Options.Workers[name];
        var source = ctx.LogSources.FirstOrDefault(s => s.Name == w.Source)
            ?? throw new InvalidOperationException($"no log source named {w.Source}: add its provider to the host (AddOpenObserve, ...)");
        var board = ctx.Board;
        var log = ctx.Logs.CreateLogger($"watch:{name}");
        var until = DateTimeOffset.UtcNow;
        var since = state.ReadUntil(name) ?? until.AddHours(-(w.Lookback ?? 24));
        var notProd = w.NotProd ?? [.. NotProdDefault];
        var max = w.MaxStories ?? 5;

        var groups = ErrorGroup.Of(await source.Errors(since, until, MaxLines, ct));
        await using var file = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await file.WriteLineAsync($"== watch {name}: {groups.Count} error group(s) in {source.Name} since {since:yyyy-MM-dd HH:mm} UTC");
        if (groups.Count == 0)
        {
            state.Read(name, until);
            return new JobOutcome("no errors logged");
        }

        var filed = (await board.Labelled(Labels.Telemetry, ct))
            .Select(s => (Story: s, Fingerprint: WatchStory.FingerprintIn(s.Body)))
            .Where(s => s.Fingerprint is not null)
            .ToLookup(s => s.Fingerprint!, s => s.Story);

        var opened = new List<int>();
        var counted = new List<int>();
        var left = new List<ErrorGroup>();
        foreach (var g in groups)
        {
            var stories = filed[g.Fingerprint].OrderBy(s => s.Number).ToList();
            if (stories.FirstOrDefault(s => s.Open) is { } open)
            {
                if (await SaidToday(open.Number, g.Fingerprint, ct)) continue;
                await board.Comment(open.Number, WatchStory.Count(g, ctx.Texts.Signature));
                counted.Add(open.Number);
                continue;
            }
            var closed = stories.Where(s => s.ClosedAt is not null).MaxBy(s => s.ClosedAt);
            // A story closed as not planned says this error is not a bug: it is never filed again.
            if (closed is { Muted: true }) continue;
            // Lines from before the story closed are the bug it closed for, not a new one.
            if (closed is not null && g.Last <= closed.ClosedAt) continue;
            if (opened.Count >= max)
            {
                left.Add(g);
                continue;
            }
            var prod = g.Cells.Any(c => ErrorGroup.IsProd(c, notProd));
            var area = AreaOf(g.Service, w.Areas);
            var n = await board.CreateStory(WatchStory.Title(g, area?[Labels.AreaPrefix.Length..] ?? g.Service.ToLowerInvariant()), WatchStory.Body(g, prod, closed), ct);
            var labels = new List<string> { Labels.Telemetry };
            if (area is not null) labels.Add(area);
            if (prod) labels.Add(Labels.Bug);
            await board.AddLabels(n, [.. labels]);
            await board.TrySetStage(n, Stages.Analyze, log, ct);
            opened.Add(n);
            await file.WriteLineAsync($"#{n}: {g.Count}× {g.Template} ({string.Join(", ", g.Cells)}){(closed is not null ? $" — again after #{closed.Number}" : "")}");
        }

        // What did not fit this pass is read again on the next one: the watch reads from its oldest line.
        state.Read(name, left.Count == 0 ? until : left.Min(g => g.First).AddTicks(-1));
        var said = new List<string>();
        if (opened.Count > 0) said.Add($"filed {string.Join(", ", opened.Select(n => $"#{n}"))}");
        if (counted.Count > 0) said.Add($"counted on {string.Join(", ", counted.Select(n => $"#{n}"))}");
        if (left.Count > 0) said.Add($"{left.Count} left for the next pass");
        return new JobOutcome(said.Count == 0 ? $"{groups.Count} error group(s), all filed and counted today" : string.Join("; ", said));
    }

    async Task<bool> SaidToday(int story, string fingerprint, CancellationToken ct)
    {
        var marker = WatchStory.CountMarker(fingerprint);
        var day = DateTimeOffset.UtcNow.AddDays(-1);
        return (await ctx.Board.Comments(story, ct)).Any(c => c.At > day && c.Body.Contains(marker, StringComparison.Ordinal));
    }

    // The area a service's story wears: the worker's own map first, then the product of that name.
    string? AreaOf(string service, IReadOnlyDictionary<string, string> areas)
    {
        if (areas.TryGetValue(service, out var area)) return area;
        return ctx.Options.Products.Values
            .FirstOrDefault(p => string.Equals(p.Name, service, StringComparison.OrdinalIgnoreCase))?.Labels
            .FirstOrDefault(l => l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal));
    }
}
