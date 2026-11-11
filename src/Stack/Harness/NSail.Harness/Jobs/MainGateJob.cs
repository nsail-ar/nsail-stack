// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using NSail.Harness.Board;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Jobs;

// main's own gate: the project's gate command on main's head, diffed from the last head it found green
// (the whole build the first time). Whatever lands on main — a merge, a dependency a release moved, a
// hand's push — is judged before the next round builds on it. Red files, or adds to, the one open
// red-main story of its own (OwnReds), born with the red labels and Ready, which the planner puts first;
// a landed one goes back to Ready only when the red head holds its merge.
// Its verdict is a commit status on the head it judged.
public sealed class MainGateJob(WorkItem item, JobContext ctx) : IJob
{
    public WorkItem Item { get; } = item;

    JobStep Step => ctx.StepOf(Item);

    // How long its build and tests may run once it holds its build turn: twice a PR gate's minutes.
    internal static TimeSpan Timeout(JobStep step)
    {
        return TimeSpan.FromMinutes(step.Minutes * 2);
    }

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var dir = await ctx.Workspace.Prepare(slot);
        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        var head = await Shell.Git(dir, "rev-parse", "HEAD");
        var green = StatusMark.Newest(await ctx.Repo.Statuses(ct: ct), Markers.GateContext, ok: true)?.Sha;
        // A base the clone no longer holds (a rewritten history) gates the whole build.
        if (green is not null && (await Shell.Run("git", ["cat-file", "-e", $"{green}^{{commit}}"], dir)).ExitCode != 0) green = null;
        await w.WriteLineAsync($"== gate main at {head[..8]} (since {green?[..8] ?? "nothing: the whole build"}) on {ctx.Options.Machine} slot {slot}");
        // The head is marked before the build: another machine's planner reads any mark as judged.
        await ctx.Repo.SetStatus(head, Markers.GateContext, "pending", $"main's gate running on {ctx.Options.Machine}", ct);

        var lease = await ctx.Leases.Acquire(JobContext.HolderOf(Item), Planner.Rank(Item.Tier), ct);
        ShellResult result;
        try
        {
            // The log moves as the turn comes: its silence while in line is not the build's (JobActivity).
            await w.WriteLineAsync("== build turn taken");
            var env = await ctx.LaneEnv(DateTimeOffset.UtcNow.AddMinutes(Step.Minutes), Item, Step, ct, runsCode: true);
            env["HARNESS_LEASE_HELD"] = "1";
            env["GATE_WORK"] = Path.Combine(ctx.Options.WorkRoot, "gate", $"slot-{slot}");
            Directory.CreateDirectory(env["GATE_WORK"]);
            string[] args = green is null ? [] : [green];
            result = Step.Command is { } command
                ? await ctx.Run(command, args, dir, env, Timeout(Step), w, ct, keep: GateJob.IsFailureLine, runsCode: true)
                : await ctx.Builder!.Gate(dir, green, env, Timeout(Step), w, ct, ctx.Options.RunCodeAs);
        }
        finally
        {
            ctx.Leases.Release(lease.Id);
        }

        var ok = result.ExitCode == 0;
        await ctx.Repo.SetStatus(head, Markers.GateContext, ok ? "success" : "failure", $"main's gate {(ok ? "green" : "red")} on {ctx.Options.Machine}", ct);
        if (ok) return await Green(head, dir, ct);

        var why = result.TimedOut ? "timed out" : "red";
        Reds[head] = result.TimedOut ? new HashSet<string>() : GateJob.FailureSet(result.Output, dir);
        await ReportRed(head, dir, green, why, GateJob.Failures(result.Output), logPath, ct);
        return new JobOutcome($"main {why} at {head[..8]}", Failed: true);
    }

    // A red-main story whose fix merged closes itself on the first green gate of a head that holds the
    // merge: nothing else says it is done, and left open the dispatcher sends it out again and again.
    // Only that evidence closes (Landing.Fixed); a red with no merged PR of its story is a hand's. The
    // stories it could not close come back, so the job fails loud instead of leaving the red story open.
    internal async Task<JobOutcome> Green(string head, string dir, CancellationToken ct)
    {
        var broke = await CloseFixedReds(head, dir, ct);
        return new JobOutcome($"main green at {head[..8]}{(broke.Count > 0 ? $"; could not close {string.Join(", ", broke.Select(n => $"#{n}"))}" : "")}", Failed: broke.Count > 0);
    }

    // A close that broke partway is finished by the next green gate: its comment, said once since the
    // merge, is not said again.
    async Task<List<int>> CloseFixedReds(string head, string dir, CancellationToken ct)
    {
        var log = ctx.Logs.CreateLogger("gate-main");
        var broke = new List<int>();
        foreach (var story in await OwnReds(ct))
        {
            try
            {
                var merge = await ctx.Repos.LandingOf(story.Number, ct);
                if (Landing.Fixed(await ctx.Repos.PrsOf(story.Number, "open", ct), merge, await Holds(merge, head, dir), head) is not { } evidence) continue;
                var said = (await ctx.Board.Comments(story.Number, ct)).Any(c => c.At >= merge!.At && c.Body.Contains(Markers.RedFixed, StringComparison.Ordinal));
                if (!said) await ctx.Board.Comment(story.Number, $"{HarnessTexts.Fill(ctx.Texts.MainRedFixed, ("evidence", evidence))} {ctx.Texts.Signature}\n{Markers.RedFixed}");
                if (await ctx.Board.StageOf(story.Number, ct) != Stages.Closed) await ctx.Board.SetStage(story.Number, Stages.Closed, ct);
                await ctx.Board.Close(story.Number, ct);
                log.LogInformation("#{Story}: red main fixed — {Evidence}; closed", story.Number, evidence);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                broke.Add(story.Number);
                log.LogError("#{Story}: red main fixed but not closed: {Error}", story.Number, e.Message);
            }
        }
        return broke;
    }

    // The open red-main stories this gate filed, oldest first: titled MainRedTitle then `: `, filed by
    // the harness's login or the fleet's (the marker rule's authors), and not routed by their area labels
    // to another repository. A story merely named like one is a hand's or another repository's: the gate
    // neither comments on it, moves it nor closes it.
    async Task<List<OpenStory>> OwnReds(CancellationToken ct)
    {
        var fleet = new HashSet<string>(ctx.Options.MarkerAuthors, StringComparer.OrdinalIgnoreCase) { await ctx.Repo.Login(ct) };
        return (await ctx.Board.OpenTitled($"{ctx.Texts.MainRedTitle}: ", ct))
            .Where(s => fleet.Contains(s.Author) && ctx.Repos.RoutedBy(s.Labels) == ctx.Repos.Default)
            .ToList();
    }

    // Whether the gated head holds the merge: the one evidence of a fix, on the green side and the red.
    static async Task<bool> Holds(Merge? merge, string head, string dir)
    {
        return merge is not null && (await Shell.Run("git", ["merge-base", "--is-ancestor", merge.Sha, head], dir)).ExitCode == 0;
    }

    // What main's gate found red at each head it judged here: a PR's gate reads it to tell main's own
    // red from the PR's. Kept in memory only: after a restart a PR's red is read as its own.
    static readonly ConcurrentDictionary<string, IReadOnlySet<string>> Reds = new();

    internal static IReadOnlySet<string>? RedAt(string sha)
    {
        return Reds.TryGetValue(sha, out var failed) ? failed : null;
    }

    internal async Task ReportRed(string head, string dir, string? since, string why, string failed, string logPath, CancellationToken ct)
    {
        var title = ctx.Texts.MainRedTitle;
        var range = since is null ? $"`{head}`" : $"`{since[..8]}..{head[..8]}`";
        var report = HarnessTexts.Fill(ctx.Texts.MainRed, ("why", why), ("sha", head), ("range", range), ("failed", failed), ("log", logPath), ("machine", ctx.Options.Machine));
        var body = $"{report}\n\n{ctx.Texts.Signature}";
        if ((await OwnReds(ct)).FirstOrDefault() is { Number: var n })
        {
            var stage = await ctx.Board.StageOf(n);
            if (!Stages.Landed.Contains(stage))
            {
                await ctx.Board.Comment(n, body);
                return;
            }
            // A head without the story's merge carries no news about its fix: a gate that started before
            // the merge reports the red it fixed. With no merge there is no fix, and any red reopens it.
            if (await ctx.Repos.LandingOf(n, ct) is { } merge && !await Holds(merge, head, dir))
            {
                var stays = HarnessTexts.Fill(ctx.Texts.MainRedWithoutFix, ("sha", head[..8]), ("merge", merge.Sha[..8]), ("stage", stage));
                await ctx.Board.Comment(n, $"{report}\n\n{stays}\n\n{ctx.Texts.Signature}");
                return;
            }
            await ctx.Board.Comment(n, body);
            await ctx.Board.SetStage(n, Stages.Ready);
            return;
        }
        var first = failed.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "the build";
        var created = await ctx.Board.CreateStory($"{title}: {(first.Length > 120 ? first[..120] : first)}", body, ctx.Options.RedLabels.ToArray());
        await ctx.Board.SetStage(created, Stages.Ready);
    }
}
