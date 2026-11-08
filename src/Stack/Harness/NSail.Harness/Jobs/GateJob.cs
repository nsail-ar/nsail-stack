// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Jobs;

// The build gate, deterministic and the harness's own — never the builder's word: the PR's head
// merged with today's main, restored, built and tested (the affected set) by the project's gate
// command, or the repository's builder when the worker names none, in a slot the last build left warm. The verdict is a PR comment carrying the gate's marker
// (Markers) — the PR itself, readable by any harness — and, when the token may, a commit status too.
// A red one names what failed, and the planner sends the story to a round.
public sealed partial class GateJob(WorkItem item, JobContext ctx) : IJob
{
    public WorkItem Item { get; } = item;

    JobStep Step => ctx.StepOf(Item);

    // How long its build and tests may run, from the moment it holds its build turn.
    internal static TimeSpan Timeout(JobStep step)
    {
        return TimeSpan.FromMinutes(step.Minutes);
    }

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var prNumber = Item.Pr!.Value;
        var log = ctx.Logs.CreateLogger($"gate#{prNumber}");
        var pr = await ctx.Repo.Pr(prNumber, ct);
        if (!pr.Open) return new JobOutcome($"PR #{prNumber} is not open", Idle: true);
        var sha = pr.HeadSha;

        try
        {
            return await Gate(prNumber, sha, slot, logPath, log, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // A gate that broke says so on the head, so nobody reads its silence as a verdict.
            await Verdict(prNumber, sha, green: false, HarnessTexts.Fill(ctx.Texts.GateBroke, ("error", e.Message)), null, log);
            throw;
        }
    }

    async Task<JobOutcome> Gate(int prNumber, string sha, int slot, string logPath, ILogger log, CancellationToken ct)
    {
        var dir = await ctx.Workspace.Prepare(slot);
        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await w.WriteLineAsync($"== gate PR #{prNumber} at {sha[..8]} on {ctx.Options.Machine} slot {slot}");

        var main = await Shell.Git(dir, "rev-parse", ctx.Workspace.Tip);
        await Shell.Git(dir, "checkout", "-q", "--detach", "-f", sha);
        var merge = await Shell.Run("git", ["merge", "--no-edit", "-q", ctx.Workspace.Tip], dir, log: w);
        if (merge.ExitCode != 0)
        {
            await Shell.Run("git", ["merge", "--abort"], dir);
            await Verdict(prNumber, sha, green: false, ctx.Texts.GateConflict, null, log);
            return new JobOutcome("red — conflicts with main", Failed: true);
        }

        // The gate takes its build turn before its clock starts: time waiting in line is not the build's.
        var lease = await ctx.Leases.Acquire(JobContext.HolderOf(Item), Planner.Rank(Item.Tier), ct);
        ShellResult result;
        try
        {
            // The log moves as the turn comes: its silence while in line is not the build's (JobActivity).
            await w.WriteLineAsync("== build turn taken");
            var env = await ctx.LaneEnv(DateTimeOffset.UtcNow.AddMinutes(Step.Minutes), Item, Step, ct, runsCode: true);
            env["HARNESS_LEASE_HELD"] = "1";
            env["GATE_WORK"] = Path.Combine(ctx.Options.WorkRoot, "gate", $"slot-{slot}");
            env["TEST_OUTPUT"] = Path.Combine(env["GATE_WORK"], "test-output.txt");
            Directory.CreateDirectory(env["GATE_WORK"]);
            result = Step.Command is { } command
                ? await ctx.Run(command, [main], dir, env, Timeout(Step), w, ct)
                : await ctx.Builder!.Gate(dir, main, env, Timeout(Step), w, ct);
        }
        finally
        {
            ctx.Leases.Release(lease.Id);
        }

        if (result.ExitCode == 0)
        {
            await Verdict(prNumber, sha, green: true, ctx.Texts.GateGreen, null, log);
            return new JobOutcome("green");
        }
        // A red that is all main's own is not the PR's round: it waits for main to move.
        if (!result.TimedOut && MainGateJob.RedAt(main) is { } mains && FailureSet(result.Output, dir) is { Count: > 0 } own && own.IsSubsetOf(mains))
        {
            await Waits(prNumber, sha, main, log);
            return new JobOutcome($"red only where main is red at {main[..8]} — gated again once main moves", Idle: true);
        }
        var why = result.TimedOut ? HarnessTexts.Fill(ctx.Texts.GateTimedOut, ("minutes", Step.Minutes)) : ctx.Texts.GateRedTests;
        await Verdict(prNumber, sha, green: false, HarnessTexts.Fill(ctx.Texts.GateRed, ("why", why)), Failures(result.Output), log);
        return new JobOutcome($"red — {why}", Failed: true);
    }

    async Task Verdict(int pr, string sha, bool green, string text, string? detail, ILogger log)
    {
        var body = HarnessTexts.Fill(ctx.Texts.GateVerdict, ("word", green ? "green" : "red"), ("sha", sha[..8]), ("text", text))
            + (detail is null ? "" : $"\n\n```\n{detail}\n```")
            + $"\n\n{ctx.Texts.Signature}\n{Markers.Gate(green, sha)}";
        await ctx.Repo.Comment(pr, body);
        try
        {
            await ctx.Repo.SetStatus(sha, Markers.GateContext, green ? "success" : "failure", green ? "build and affected tests green" : "red: see the PR comment");
        }
        catch (ProviderException e)
        {
            // The comment is the verdict; the status is a courtesy the token may not be allowed.
            log.LogDebug("no gate status on {Sha}: {Error}", sha[..8], e.Message);
        }
    }

    async Task Waits(int pr, string sha, string main, ILogger log)
    {
        var body = HarnessTexts.Fill(ctx.Texts.GateVerdict, ("word", "red"), ("sha", sha[..8]), ("text", HarnessTexts.Fill(ctx.Texts.GateWaits, ("main", main[..8]))))
            + $"\n\n{ctx.Texts.Signature}\n{Markers.GateWaits(sha, main)}";
        await ctx.Repo.Comment(pr, body);
        try
        {
            await ctx.Repo.SetStatus(sha, Markers.GateContext, "pending", $"red only where main is red at {main[..8]}");
        }
        catch (ProviderException e)
        {
            log.LogDebug("no gate status on {Sha}: {Error}", sha[..8], e.Message);
        }
    }

    // What failed, comparable across slots and runs: the failed tests by name, else the build errors,
    // with the slot's own path and a test's time taken out.
    internal static IReadOnlySet<string> FailureSet(string output, string dir)
    {
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r').Replace(dir.TrimEnd('/', '\\'), "", StringComparison.Ordinal).Trim()).ToList();
        var failed = lines.Where(l => FailedTest().IsMatch(l)).Select(l => TestTime().Replace(l, "")).ToHashSet();
        return failed.Count > 0 ? failed : lines.Where(l => BuildError().IsMatch(l)).ToHashSet();
    }

    // What a builder needs to start: the failed test names, else the build errors, else the tail.
    internal static string Failures(string output)
    {
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var failed = lines.Where(l => FailedTest().IsMatch(l)).Select(l => l.Trim()).Distinct().Take(30).ToList();
        if (failed.Count > 0) return string.Join('\n', failed);
        var errors = lines.Where(l => BuildError().IsMatch(l)).Select(l => l.Trim()).Distinct().Take(30).ToList();
        if (errors.Count > 0) return string.Join('\n', errors);
        return string.Join('\n', lines.Where(l => l.Length > 0).TakeLast(30));
    }

    [GeneratedRegex(@"^\s*Failed \S")] private static partial Regex FailedTest();
    [GeneratedRegex(@": error [A-Z]+\d+:")] private static partial Regex BuildError();
    [GeneratedRegex(@"\s*\[[\d.,]+ m?s\]$")] private static partial Regex TestTime();
}
