// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Harness.Agents;
using NSail.Harness.Board;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Jobs;

// The analyst shapes one story in Analyze into what a cold builder needs, or reads why a
// `stalled` one died. Four ways out: no question fired a trigger -> Ready; one did -> stays in
// Analyze wearing `ruling` with only that question on the thread (never a release red's: its ruling is
// refused and counted as a death, since the release waits for no hand); a Ready story whose change must
// touch a path the screen holds (its protected.txt) -> Ready wearing `ruling` and `screened` until a
// hand adds `allow-protected`; the story is already built (its PR merged and main meets every
// criterion) -> Merged, so no empty voyage is sent; the story is several that must land apart (a
// change in one repository and its use in another) -> its parts are filed under it and it is built
// through them (never a release red or a stalled story: those are one fix). A death with no verdict is
// sent again on the next pass; the second in a row (or the first of a story that was already stalled)
// stamps `stalled` and `ruling`, and it is not taken again.
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
            // An analysis's out folder is its own: what an earlier story left there (a stalled mark, a
            // verdict) would be read as this one's.
            var outDir = Path.Combine(dir, Out);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
            Directory.CreateDirectory(outDir);
            await File.WriteAllTextAsync(Path.Combine(outDir, "original.md"), $"{StoryDossier.Heading(ctx.Names.Ref(n), title, labels, body)}\n\n{StoryDossier.Filed(body, labels)}", ct);
            var dossier = new StoryDossier(ctx, history);
            await File.WriteAllTextAsync(Path.Combine(outDir, "thread.md"), StoryDossier.Thread(await board.Comments(n, ct), await dossier.Trusted(ct)), ct);
            await File.WriteAllTextAsync(Path.Combine(outDir, "labels-known.txt"), string.Join(' ', await KnownLabels(ct)), ct);
            if (stalled) await File.WriteAllTextAsync(Path.Combine(outDir, "stalled.txt"), "stalled", ct);
            await dossier.WritePrs(n, outDir, ct);
            if (stalled) await dossier.WriteRuns(n, outDir, ct);

            var env = await ctx.LaneEnv(started.AddMinutes(Step.Minutes), Item, Step, ct);
            await using (var w = new StreamWriter(logPath, append: true) { AutoFlush = true })
            {
                await w.WriteLineAsync(LogHeading(ctx.Names.Ref(n), title, stalled, ctx.Options.Machine, slot, labels, body));
                await ctx.Prepare(dir, env, w);
            }

            var prompt = await SeatPrompt.Read(dir, Seat, new Dictionary<string, string>
            {
                ["ISSUE"] = n.ToString(CultureInfo.InvariantCulture),
                ["REPO"] = ctx.Repo.Name,
                ["OUT"] = Out,
                ["PROTECTED"] = string.Join(", ", ctx.Options.ProtectedPaths.Select(p => $"`{p}`")),
            }, ct);
            // The seat reads the board from disk and writes its verdict to disk: it holds no token to the board.
            var result = await ctx.RunSeat(new AgentRun(
                prompt, dir, Seat.Model,
                Access: AgentAccess.Reader,
                MaxTurns: 300,
                Timeout: TimeSpan.FromMinutes(Step.Minutes),
                Env: ctx.SeatEnv(env),
                LogPath: logPath,
                Marketplace: dir), ct);

            var ending = await Act(n, title, outDir, stalled, result, log, ct);
            return ending.Outcome(result.QuotaRefused ? result.QuotaUntil ?? DateTimeOffset.UtcNow.AddHours(1) : null);
        }
        finally
        {
            await board.RemoveLabel(n, Labels.Analyzing);
        }
    }

    // The job log's first line; a Watch story's title is log text, so it follows fenced, as in original.md.
    internal static string LogHeading(string reference, string title, bool stalled, string machine, int slot, IEnumerable<string> labels, string body)
    {
        var tail = $"{(stalled ? " (stalled)" : "")} on {machine} slot {slot}";
        return StoryDossier.IsWatch(labels, body) ? $"== analyze {reference}{tail}\n{Untrusted.Fence(title)}" : $"== analyze {reference} {title}{tail}";
    }

    async Task<Ending> Act(int n, string title, string outDir, bool stalled, AgentResult result, ILogger log, CancellationToken ct)
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
                var allowed = SeatOutput.AllowedLabels(Read("labels.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), await KnownLabels(ct));
                foreach (var label in allowed)
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
                await WrittenBlockers.Link(board, n, $"{Read("body.md")}\n{comment}", ctx.Names.Key, ct, ctx.Repos);
                await Hold(n, Read("protected.txt"), ct);
                // The move is the Ready event's (WorkerTypes), once the lock is off.
                return new Ending("Ready", "Ready");
            case "VERDICT: split" when !stalled && !ctx.Texts.IsRed(title) && comment.Trim().Length > 0 && StoryParts.Read(outDir) is { } parts:
                return await Split(n, parts, Post, ct);
            case "VERDICT: ruling" when ctx.Texts.IsRed(title):
                // Counted as a death below: a seat that asks twice is stalled, loudly, not obeyed.
                await board.Comment(n, $"{ctx.Texts.RedRulingRefused} {ctx.Texts.Signature}");
                break;
            case "VERDICT: ruling" when comment.Trim().Length > 0:
                await Post();
                await WrittenBlockers.Link(board, n, comment, ctx.Names.Key, ct, ctx.Repos);
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
        var deaths = ctx.Markers.InARow(await board.Comments(n, ct), Markers.AnalysisDied, await ctx.Repo.Login(ct), since) + 1;
        if (stalled || deaths >= 2)
        {
            await board.AddLabels(n, Labels.Stalled, Labels.Ruling);
            await board.Comment(n, JobContext.Signed(HarnessTexts.Fill(ctx.Texts.AnalysisDiedTwice, ("machine", ctx.Options.Machine)), Seat) + $"\n{Markers.AnalysisDied}");
            return new Ending("died twice — stalled and ruling", Failed: true);
        }
        await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.AnalysisDied, ("machine", ctx.Options.Machine))} {ctx.Texts.Signature}\n{Markers.AnalysisDied}");
        return new Ending("no verdict — sent again on the next pass", Failed: true);
    }

    // Each part is filed under the story in order, so a later one can name an earlier one. A part that
    // waits on another story goes to Backlog blocked, and is analyzed once what it waits on has landed;
    // one with its refinery comment goes to Ready; any other to Analyze. The parts inherit the story's
    // area and priority. The story's own move is the Split event's (WorkerTypes).
    async Task<Ending> Split(int n, IReadOnlyList<StoryPart> parts, Func<Task> post, CancellationToken ct)
    {
        var board = ctx.Board;
        var known = (await KnownLabels(ct)).ToList();
        var inherited = (await board.Labels(n, ct)).Where(l => l == Labels.P1 || l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal)).ToList();
        var filed = new List<int>();
        string Filed()
        {
            return string.Join(", ", filed.Select(f => $"#{f}"));
        }
        try
        {
            foreach (var part in parts)
            {
                var body = StoryParts.Resolve(part.Body, filed);
                var refinery = StoryParts.Resolve(part.Comment, filed);
                var shaped = refinery.Trim().Length > 0;
                var child = await board.CreateStory(part.Title, body, [.. inherited.Union(SeatOutput.AllowedLabels(part.Labels, known))], ct);
                filed.Add(child);
                await board.AddChild(n, child, ct);
                if (shaped) await board.Comment(child, refinery);
                var waits = await WrittenBlockers.Link(board, child, $"{body}\n{refinery}", ctx.Names.Key, ct, ctx.Repos);
                if (waits.Count == 0 && shaped) await Hold(child, part.Protected, ct);
                await board.SetStage(child, waits.Count > 0 ? Stages.Backlog : shaped ? Stages.Ready : Stages.Analyze, ct);
            }
        }
        // A split cut short is not sent again: the next analysis would file the first parts twice.
        catch (Exception e) when (filed.Count > 0 && e is ProviderException or HttpRequestException)
        {
            await board.AddLabels(n, Labels.Stalled, Labels.Ruling);
            await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.SplitHalfMade, ("parts", Filed()), ("error", e.Message))} {ctx.Texts.Signature}");
            return new Ending($"split broke after {filed.Count} of {parts.Count} parts — stalled and ruling", Failed: true);
        }
        await post();
        await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.SplitInto, ("parts", Filed()))} {ctx.Texts.Signature}");
        return new Ending($"split into {Filed()}", "Split");
    }

    // A story shaped to touch a path the screen holds waits for `allow-protected` instead of a voyage
    // the screen refuses.
    async Task Hold(int n, string listed, CancellationToken ct)
    {
        if (await Screen(n, listed, ct) is not { Count: > 0 } screened) return;
        await ctx.Board.AddLabels(n, Labels.Ruling, Labels.Screened);
        await ctx.Board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.ProtectedAhead, ("paths", string.Join(", ", screened.Select(f => $"`{f}`"))), ("label", Labels.AllowProtected))} {ctx.Texts.Signature}");
    }

    // The paths of the analyst's protected.txt the screen holds, none once a hand or the harness lifted it.
    async Task<IReadOnlyList<string>> Screen(int n, string listed, CancellationToken ct)
    {
        if (ctx.Options.AllowProtectedPaths || (await ctx.Board.Labels(n, ct)).Contains(Labels.AllowProtected)) return [];
        return SeatOutput.Protected(listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => p.Trim('`')), ctx.Options.ProtectedPaths).Distinct().ToList();
    }

    async Task<IEnumerable<string>> KnownLabels(CancellationToken ct)
    {
        return (await ctx.Board.KnownLabels(ct)).Where(l => l == Labels.NoWalk || l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal) || l.StartsWith(ctx.Agent.Driver + ":", StringComparison.Ordinal));
    }
}
