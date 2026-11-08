// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Jobs;

// main's own gate: the project's gate command on main's head, diffed from the last head it found green
// (the whole build the first time). Whatever lands on main — a merge, a dependency a release moved, a
// hand's push — is judged before the next round builds on it. Red files, or adds to, the one open
// red-main story, born with the red labels and Ready, which the planner puts first.
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
                ? await ctx.Run(command, args, dir, env, Timeout(Step), w, ct)
                : await ctx.Builder!.Gate(dir, green, env, Timeout(Step), w, ct);
        }
        finally
        {
            ctx.Leases.Release(lease.Id);
        }

        var ok = result.ExitCode == 0;
        await ctx.Repo.SetStatus(head, Markers.GateContext, ok ? "success" : "failure", $"main's gate {(ok ? "green" : "red")} on {ctx.Options.Machine}", ct);
        if (ok)
        {
            var broke = await CloseFixedReds(head, dir, ct);
            return new JobOutcome($"main green at {head[..8]}{(broke.Count > 0 ? $"; could not close {string.Join(", ", broke.Select(n => $"#{n}"))}" : "")}", Failed: broke.Count > 0);
        }

        var why = result.TimedOut ? "timed out" : "red";
        Reds[head] = result.TimedOut ? new HashSet<string>() : GateJob.FailureSet(result.Output, dir);
        await ReportRed(head, green, why, GateJob.Failures(result.Output), logPath);
        return new JobOutcome($"main {why} at {head[..8]}", Failed: true);
    }

    // A red-main story whose fix merged closes itself on the first green gate of a head that holds the
    // merge: nothing else says it is done, and left open the dispatcher sends it out again and again.
    // Only that evidence closes (Landing.Fixed); a red with no merged PR of its story is a hand's. The
    // stories it could not close come back, so the job fails loud instead of leaving the red story open.
    async Task<List<int>> CloseFixedReds(string head, string dir, CancellationToken ct)
    {
        var log = ctx.Logs.CreateLogger("gate-main");
        var broke = new List<int>();
        foreach (var story in (await ctx.Board.Stories(ct)).Where(i => i.Title.StartsWith(ctx.Texts.MainRedTitle, StringComparison.Ordinal)))
        {
            try
            {
                var merge = await ctx.Repo.MergeOf(story.Number, ct);
                var holds = merge is not null && (await Shell.Run("git", ["merge-base", "--is-ancestor", merge.Sha, head], dir)).ExitCode == 0;
                if (Landing.Fixed(await ctx.Repos.PrsOf(story.Number, "open", ct), merge, holds, head) is not { } evidence) continue;
                await ctx.Board.Comment(story.Number, $"{HarnessTexts.Fill(ctx.Texts.MainRedFixed, ("evidence", evidence))} {ctx.Texts.Signature}");
                await ctx.Board.SetStage(story.Number, Stages.Closed, ct);
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

    // What main's gate found red at each head it judged here: a PR's gate reads it to tell main's own
    // red from the PR's. Kept in memory only: after a restart a PR's red is read as its own.
    static readonly ConcurrentDictionary<string, IReadOnlySet<string>> Reds = new();

    internal static IReadOnlySet<string>? RedAt(string sha)
    {
        return Reds.TryGetValue(sha, out var failed) ? failed : null;
    }

    async Task ReportRed(string head, string? since, string why, string failed, string logPath)
    {
        var title = ctx.Texts.MainRedTitle;
        var range = since is null ? $"`{head}`" : $"`{since[..8]}..{head[..8]}`";
        var body = HarnessTexts.Fill(ctx.Texts.MainRed, ("why", why), ("sha", head), ("range", range), ("failed", failed), ("log", logPath), ("machine", ctx.Options.Machine))
            + $"\n\n{ctx.Texts.Signature}";
        if (await ctx.Board.FindOpen(title, exact: false) is { } n)
        {
            await ctx.Board.Comment(n, body);
            var stage = await ctx.Board.StageOf(n);
            if (Stages.Landed.Contains(stage)) await ctx.Board.SetStage(n, Stages.Ready);
            return;
        }
        var first = failed.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "the build";
        var created = await ctx.Board.CreateStory($"{title}: {(first.Length > 120 ? first[..120] : first)}", body);
        await ctx.Board.AddLabels(created, ctx.Options.RedLabels.ToArray());
        await ctx.Board.SetStage(created, Stages.Ready);
    }
}
