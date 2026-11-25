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
// A red one names what failed, and the planner sends the story to a round; one red by time alone, with
// nothing failed, calls a hand instead (OutOfTime).
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
        // The plan read the thread before this job held the story: another harness may have judged the head since.
        if (Markers.VerdictAt(await Bodies(prNumber, ct), sha) is { } said)
        {
            log.LogInformation("PR #{Pr}: {Said} at {Sha} already — no gate", prNumber, said, sha[..8]);
            return new JobOutcome($"{said.ToString().ToLowerInvariant()} at {sha[..8]} already — no gate", Idle: true);
        }

        try
        {
            return await Gate(prNumber, sha, slot, logPath, log, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && IsOwnRed(e))
        {
            var unreadable = InCheckout(e.Message, ctx.Workspace.SlotPath(slot));
            return await Verdict(prNumber, sha, green: false, HarnessTexts.Fill(ctx.Texts.GateRed, ("why", ctx.Texts.GateUnreadable)), unreadable,
                "red", new JobOutcome($"red — {unreadable}", Failed: true), log, ct);
        }
        // A workspace the machine cannot prepare is said in the machine's log alone (HarnessLoop.Sail).
        catch (Exception e) when (!ct.IsCancellationRequested && e is not (WorkspaceFault or SharedStepException or MachineSaturated))
        {
            // Infrastructure is not the PR's red: the comment carries no gate marker and no status is set,
            // so the head stays ungated and the next pass gates it again.
            await Broke(prNumber, sha, e.Message, log);
            throw;
        }
    }

    async Task<IReadOnlyList<string>> Bodies(int prNumber, CancellationToken ct)
    {
        return [.. ctx.Markers.Bodies(await ctx.Repo.Thread(prNumber, ct), await ctx.Repo.Login(ct))];
    }

    // A message as the checkout would say it: the slot's own path is the machine's, never the PR's to read.
    internal static string InCheckout(string message, string dir)
    {
        var root = dir.TrimEnd('/', '\\');
        foreach (var form in new[] { root, root.Replace('\\', '/'), root.Replace('/', '\\') })
        {
            message = message.Replace(form + "/", "", StringComparison.Ordinal).Replace(form + "\\", "", StringComparison.Ordinal).Replace(form, "", StringComparison.Ordinal);
        }
        return message;
    }

    // The builder reads the PR's own files (solution, projects) before it builds: a malformed project or
    // a missing solution is the PR's red; any other exception (git, provider, disk) is infrastructure.
    internal static bool IsOwnRed(Exception e)
    {
        return e is System.Xml.XmlException or FileNotFoundException or DirectoryNotFoundException;
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
            return await Verdict(prNumber, sha, green: false, ctx.Texts.GateConflict, null, "red", new JobOutcome("red — conflicts with main", Failed: true), log, ct);
        }

        // The gate takes its build turn before its clock starts: time waiting in line is not the build's.
        var lease = await ctx.Leases.Acquire(JobContext.HolderOf(Item), Planner.Rank(Item.Tier), BuildTurn.Gate, ct);
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
                ? await ctx.Run(command, [main], dir, env, Timeout(Step), w, ct, keep: IsKeptLine, runsCode: true)
                : await ctx.Builder!.Gate(dir, main, env, Timeout(Step), w, ct, ctx.Options.RunCodeAs);
        }
        finally
        {
            ctx.Leases.Release(lease.Id);
        }
        return await Judge(prNumber, sha, main, dir, result, log, ct);
    }

    // What a run says of the head. A clock run out on a machine the kernel reports saturated is the
    // machine's, once per head: said as infrastructure (no verdict, no status) and gated again after
    // the backoff. Any other run out of time with nothing red is red by time alone (OutOfTime).
    internal async Task<JobOutcome> Judge(int prNumber, string sha, string main, string dir, ShellResult result, ILogger log, CancellationToken ct)
    {
        if (result.TimedOut && ctx.Options.Pressure.Saturated(ctx.Pressure.Read()) is { } reading
            && await Bodies(prNumber, ct) is var bodies && !Markers.SaturatedAt(bodies, sha))
        {
            if (Dropped(bodies, prNumber, sha, "a run out of time", log) is { } dropped) return dropped;
            var error = HarnessTexts.Fill(ctx.Texts.GateSaturated, ("minutes", Step.Minutes), ("reading", reading));
            await Broke(prNumber, sha, error, log, Markers.GateSaturated(sha));
            throw new MachineSaturated(error);
        }
        if (result.ExitCode == 0)
        {
            return await Verdict(prNumber, sha, green: true, ctx.Texts.GateGreen, null, "green", new JobOutcome("green"), log, ct);
        }
        var failures = FailureSet(result.Output, dir);
        if (result.TimedOut && failures.Count == 0) return await OutOfTime(prNumber, sha, result.Output, log, ct);
        // A red that is all main's own is not the PR's round: it waits for main to move.
        if (MainGateJob.RedAt(main) is { } mains && failures.Count > 0 && failures.IsSubsetOf(mains))
        {
            return await Waits(prNumber, sha, main, log, ct);
        }
        var why = ctx.Texts.GateRedTests;
        return await Verdict(prNumber, sha, green: false, HarnessTexts.Fill(ctx.Texts.GateRed, ("why", why)), Failures(result.Output),
            "red", new JobOutcome($"red — {why}", Failed: true), log, ct);
    }

    // Red by time alone: the clock ran out with nothing red. The verdict says what was green and fires
    // OutOfTime (the type's default calls a hand) instead of a round: no builder can fix a clock.
    async Task<JobOutcome> OutOfTime(int prNumber, string sha, string output, ILogger log, CancellationToken ct)
    {
        var late = HarnessTexts.Fill(ctx.Texts.GateTimedOut, ("minutes", Step.Minutes));
        var text = HarnessTexts.Fill(ctx.Texts.GateOutOfTime, ("late", late), ("green", GreenOf(output, ctx.Texts)));
        return await Verdict(prNumber, sha, green: false, text, null, "a run out of time", new JobOutcome($"red — {late}", Event: OutOfTimeEvent, Failed: true), log, ct);
    }

    // The Verify event a head red by time alone fires.
    public const string OutOfTimeEvent = "OutOfTime";

    // What a run cut by its clock had finished green: the build, and each test project that passed.
    internal static string GreenOf(string output, HarnessTexts texts)
    {
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var suites = lines.Select(l => PassedSuite().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct().ToList();
        if (suites.Count > 0) return HarnessTexts.Fill(texts.GateTestsGreen, ("count", suites.Count), ("names", string.Join(", ", suites)));
        return lines.Any(l => BuildSucceeded().IsMatch(l)) ? texts.GateBuildGreen : texts.GateNothingGreen;
    }

    // A head judged while this run built (another harness's verdict) keeps that verdict: this run's word is
    // dropped, said in the log alone — no comment, no status, no event. Every marker this job writes reads the thread first.
    static JobOutcome? Dropped(IEnumerable<string> bodies, int pr, string sha, string word, ILogger log)
    {
        if (Markers.VerdictAt(bodies, sha) is not { } said) return null;
        log.LogWarning("PR #{Pr}: {Word} at {Sha} dropped — the head already reads {Said}", pr, word, sha[..8], said);
        return new JobOutcome($"{word} dropped — {said.ToString().ToLowerInvariant()} at {sha[..8]} already", Idle: true);
    }

    // The verdict, unless the head already carries one: then the run's outcome is the drop.
    async Task<JobOutcome> Verdict(int pr, string sha, bool green, string text, string? detail, string word, JobOutcome outcome, ILogger log, CancellationToken ct)
    {
        if (Dropped(await Bodies(pr, ct), pr, sha, word, log) is { } dropped) return dropped;
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
        return outcome;
    }

    async Task Broke(int pr, string sha, string error, ILogger log, string? marker = null)
    {
        var body = HarnessTexts.Fill(ctx.Texts.GateVerdict, ("word", "broke"), ("sha", sha[..8]), ("text", HarnessTexts.Fill(ctx.Texts.GateBroke, ("error", error))))
            + $"\n\n{ctx.Texts.Signature}" + (marker is null ? "" : $"\n{marker}");
        try
        {
            await ctx.Repo.Comment(pr, body);
        }
        catch (ProviderException e)
        {
            log.LogDebug("no gate-broke comment on {Sha}: {Error}", sha[..8], e.Message);
        }
    }

    async Task<JobOutcome> Waits(int pr, string sha, string main, ILogger log, CancellationToken ct)
    {
        if (Dropped(await Bodies(pr, ct), pr, sha, "red only where main is red", log) is { } dropped) return dropped;
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
        return new JobOutcome($"red only where main is red at {main[..8]} — gated again once main moves", Idle: true);
    }

    // What failed, comparable across slots and runs: the failed tests by name, else the build errors,
    // with the slot's own path and a test's time taken out.
    internal static IReadOnlySet<string> FailureSet(string output, string dir)
    {
        var root = dir.TrimEnd('/', '\\');
        var lines = output.Split('\n').Select(l => (root.Length == 0 ? l.TrimEnd('\r') : l.TrimEnd('\r').Replace(root, "", StringComparison.Ordinal)).Trim()).ToList();
        var failed = lines.Where(l => FailedTest().IsMatch(l)).Select(l => TestTime().Replace(l, "")).ToHashSet();
        return failed.Count > 0 ? failed : lines.Where(l => BuildError().IsMatch(l)).ToHashSet();
    }

    // A line the failure sets read: kept through the output's cut, since failures come all along a run.
    internal static bool IsFailureLine(string line)
    {
        return FailedTest().IsMatch(line) || BuildError().IsMatch(line);
    }

    // What the gate reads off its output, kept through the cut: the failures, and what went green.
    internal static bool IsKeptLine(string line)
    {
        return IsFailureLine(line) || PassedSuite().IsMatch(line) || BuildSucceeded().IsMatch(line);
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

    // A gate that ran out its clock on a saturated machine: its comment is written, the job ends as a break.
    internal sealed class MachineSaturated(string message) : Exception(message);

    [GeneratedRegex(@"^\s*Failed \S")] private static partial Regex FailedTest();
    [GeneratedRegex(@": error [A-Z]+\d+:")] private static partial Regex BuildError();
    [GeneratedRegex(@"\s*\[[\d.,]+ m?s\]$")] private static partial Regex TestTime();
    [GeneratedRegex(@"^\s*Passed!\s+-.*\s-\s+(\S+?)\.dll\b")] private static partial Regex PassedSuite();
    [GeneratedRegex(@"^\s*Build succeeded\b")] private static partial Regex BuildSucceeded();
}
