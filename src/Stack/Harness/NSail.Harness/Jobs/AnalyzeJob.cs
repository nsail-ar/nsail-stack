// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Harness.Agents;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Jobs;

// The analyst shapes one story in Analyze into what a cold builder needs, or reads why a
// `stalled` one died. Three ways out: no question fired a trigger -> Ready; one did -> stays in
// Analyze wearing `ruling` with only that question on the thread; the story is already built (its
// PR merged and main meets every criterion) -> Merged, so no empty voyage is sent. A death with
// no verdict is sent again on the next pass; the second in a row (or the first of a story that
// was already stalled) stamps `stalled` and `ruling`, and it is not taken again.
public sealed class AnalyzeJob(WorkItem item, JobContext ctx, JobHistory history) : IJob
{
    const string Out = ".analysis";

    JobStep Step => ctx.StepOf(Item);

    AgentOptions Seat => Step.Agent!;

    public WorkItem Item { get; } = item;

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var n = Item.Story!.Value;
        var board = ctx.Board;
        var log = ctx.Logs.CreateLogger($"analyze#{n}");

        var (title, body, open) = await board.Story(n, ct);
        var labels = await board.Labels(n, ct);
        var stage = await board.StageOf(n, ct);
        var why = stage != Stages.Analyze ? $"its stage is {stage}, not Analyze"
            : !open ? "it is closed"
            : labels.Overlaps([Labels.Ruling, Labels.Blocked, Labels.Claimed, Labels.Review, Labels.Walking, Labels.Analyzing, Labels.Hand]) ? "it carries a hold or a lock"
            : await board.Children(n, ct) > 0 ? "it has sub-issues: a spine is built through its children"
            : null;
        if (why is not null) return new JobOutcome($"not analyzed — {why}", Idle: true);

        var stalled = labels.Contains(Labels.Stalled);
        await board.AddLabels(n, Labels.Analyzing);
        try
        {
            var started = DateTimeOffset.UtcNow;
            var dir = await ctx.Workspace.Prepare(slot);
            var outDir = Path.Combine(dir, Out);
            Directory.CreateDirectory(outDir);
            await File.WriteAllTextAsync(Path.Combine(outDir, "original.md"), $"# {title}\n\n{body}", ct);
            var thread = (await board.Comments(n, ct)).Select(c => $"--- {c.Author} {c.At:O}\n{c.Body}");
            await File.WriteAllTextAsync(Path.Combine(outDir, "thread.md"), string.Join('\n', thread), ct);
            await File.WriteAllTextAsync(Path.Combine(outDir, "labels-known.txt"), string.Join(' ', await KnownLabels(ct)), ct);
            if (stalled) await File.WriteAllTextAsync(Path.Combine(outDir, "stalled.txt"), "stalled", ct);
            var dossier = new StoryDossier(ctx, history);
            await dossier.WritePrs(n, outDir, ct);
            if (stalled) await dossier.WriteRuns(n, outDir, ct);

            var env = await ctx.LaneEnv(started.AddMinutes(Step.Minutes), Item, Step, ct);
            await using (var w = new StreamWriter(logPath, append: true) { AutoFlush = true })
            {
                await w.WriteLineAsync($"== analyze {ctx.Names.Ref(n)} {title}{(stalled ? " (stalled)" : "")} on {ctx.Options.Machine} slot {slot}");
                await ctx.Prepare(dir, env, w);
            }

            var prompt = await SeatPrompt.Read(dir, Seat, new Dictionary<string, string>
            {
                ["ISSUE"] = n.ToString(CultureInfo.InvariantCulture),
                ["REPO"] = ctx.Repo.Name,
                ["OUT"] = Out,
            }, ct);
            // The seat reads the board from disk and writes its verdict to disk: it holds no token to the board.
            var result = await ctx.Agent.Run(new AgentRun(
                prompt, dir, Seat.Model,
                Access: AgentAccess.Reader,
                MaxTurns: 300,
                Timeout: TimeSpan.FromMinutes(Step.Minutes),
                Env: ctx.SeatEnv(env),
                LogPath: logPath,
                Marketplace: dir), ct);

            var ending = await Act(n, outDir, stalled, result, log, ct);
            return ending.Outcome(result.QuotaRefused ? result.QuotaUntil ?? DateTimeOffset.UtcNow.AddHours(1) : null);
        }
        finally
        {
            await board.RemoveLabel(n, Labels.Analyzing);
        }
    }

    async Task<Ending> Act(int n, string outDir, bool stalled, AgentResult result, ILogger log, CancellationToken ct)
    {
        var board = ctx.Board;
        string Read(string f)
        {
            var path = Path.Combine(outDir, f);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        var verdict = Read("verdict.txt").Split('\n').FirstOrDefault()?.Trim() ?? "";
        var comment = Read("comment.md");
        var ruled = Read("ruled.md");

        async Task Post()
        {
            var posted = comment;
            if (ruled.Trim().Length > 0)
            {
                posted = $"{ruled}\n\n{comment}";
                if (Seat.RuledLabel is { } ruledLabel) await board.AddLabels(n, ruledLabel);
            }
            await board.Comment(n, posted);
        }

        switch (verdict)
        {
            case "VERDICT: ready" when Read("body.md").Trim().Length > 0 && comment.Trim().Length > 0:
                await ctx.Board.UpdateBody(n, Read("body.md"), ct);
                foreach (var label in Read("labels.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    try
                    {
                        await board.AddLabels(n, label);
                    }
                    catch (ProviderException)
                    {
                    }
                }
                await Post();
                // The move is the Ready event's (WorkerTypes), once the lock is off.
                return new Ending("Ready", "Ready");
            case "VERDICT: ruling" when comment.Trim().Length > 0:
                await Post();
                return new Ending("ruling", "Ruling");
            case "VERDICT: done" when comment.Trim().Length > 0:
                await Post();
                return new Ending("done", "Done");
        }

        if (result.QuotaRefused) return "quota — no verdict, sent again after the wall";

        // The second death in a row since a hand last lifted a hold is not sent a third time;
        // a story already stalled had its first death before this one.
        // Each death is said on the story with its marker, so any harness counts the same run.
        var since = await board.LastLift(n, Labels.Lifts, ct);
        var deaths = Markers.InARow(await board.Comments(n, ct), Markers.AnalysisDied, since) + 1;
        if (stalled || deaths >= 2)
        {
            await board.AddLabels(n, Labels.Stalled, Labels.Ruling);
            await board.Comment(n, JobContext.Signed(HarnessTexts.Fill(ctx.Texts.AnalysisDiedTwice, ("machine", ctx.Options.Machine)), Seat) + $"\n{Markers.AnalysisDied}");
            return new Ending("died twice — stalled and ruling", Failed: true);
        }
        await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.AnalysisDied, ("machine", ctx.Options.Machine))} {ctx.Texts.Signature}\n{Markers.AnalysisDied}");
        return new Ending("no verdict — sent again on the next pass", Failed: true);
    }

    async Task<IEnumerable<string>> KnownLabels(CancellationToken ct)
    {
        return (await ctx.Board.KnownLabels(ct)).Where(l => l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal) || l.StartsWith(ctx.Agent.Driver + ":", StringComparison.Ordinal) || l == Labels.P1);
    }
}
