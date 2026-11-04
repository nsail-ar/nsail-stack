// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Jobs;

// A product's cut, on a release branch. The day's cut opens release/{product}/{stamp} at main's
// head and runs the release worker's browser suite there (its Suite on the repository's builder, or
// its command); on green its workflow is
// pressed with that sha — the cut itself runs on the code host. With no suite, the head is cut as
// it is. A red suite still releases the last sha the suite passed, unless the product's last tag
// already holds it, and files the red as the product's one red-suite story, born with the red labels
// and Ready: the crew fixes it on main like any story.
// When that fix merges, the fix is carried to the newest release branch (cherry-picked, so main's
// newer work stays out of the cut) and only the tests that failed run again: green cuts the branch,
// red sends the story back to Ready with what still fails. A branch, a list or a pick that is not
// there falls back to the whole suite on main's head.
// Its verdicts live on the commits: the day's cut tried and the suite's word, as commit statuses.
public sealed partial class ReleaseJob(WorkItem item, JobContext ctx) : IJob
{
    public WorkItem Item { get; } = item;

    // How many release branches of a product are kept; older ones are deleted when a new one opens.
    const int BranchesKept = 3;

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var p = Item.Product!;
        var step = ctx.StepOf(Item);
        var product = step.Worker.Product ?? char.ToUpperInvariant(p[0]) + p[1..];

        var dir = await ctx.Workspace.Prepare(slot);
        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await Shell.Git(dir, "fetch", "-q", "--force", "--prune", "origin", $"+refs/tags/{p}/*:refs/tags/{p}/*", $"+refs/heads/{BranchPrefix(p)}*:refs/remotes/origin/{BranchPrefix(p)}*");
        var head = await Shell.Git(dir, "rev-parse", "HEAD");
        await ctx.Repo.SetStatus(head, Markers.CutContext(p), "success", $"cut tried on {ctx.Options.Machine}", ct);
        var marks = await ctx.Repo.Statuses(ct: ct);
        // The last release is the newest one published: a tag whose cut died before publishing holds
        // nothing that reached anyone, and read as released it would skip every later cut.
        var last = (await ctx.Repo.Releases(ct)).FirstOrDefault(r => r.Tag.StartsWith($"{p}/v", StringComparison.Ordinal))?.Tag;
        await w.WriteLineAsync($"== release {p} at {head[..8]} (last tag {last ?? "none"}) on {ctx.Options.Machine} slot {slot}");

        if (Item.Story is { } red && HasSuite(step) && await FixOnBranch(p, product, red, dir, step, w, logPath, ct) is { } carried) return carried;

        var pathsFile = step.Worker.Paths ?? throw new InvalidOperationException($"release {p} names no Paths");
        if (!File.Exists(Path.Combine(dir, pathsFile))) return new JobOutcome($"no {pathsFile}", Idle: true);
        if (last is not null)
        {
            var paths = (await File.ReadAllLinesAsync(Path.Combine(dir, pathsFile), ct)).Where(l => l.Trim().Length > 0).Select(l => l.Trim());
            var news = await Shell.Git(dir, ["log", "--format=%h", $"{last}..HEAD", "--", .. paths]);
            if (news.Trim().Length == 0) return new JobOutcome($"nothing new for {p} since {last}", Idle: true);
        }

        if (HasSuite(step) && StatusMark.Newest(marks, Markers.SuiteContext(p)) is { } ran && ran.Sha == head)
        {
            if (!ran.Ok) return new JobOutcome($"the suite is already red at {head[..8]} — waits for a new head", Idle: true);
        }
        else if (HasSuite(step))
        {
            var branch = await OpenBranch(p, dir, w);
            var result = await Suite(product, null, dir, step, w, ct);
            var why = result.ExitCode == 0 ? "green" : result.TimedOut ? $"passed {step.Minutes} min" : "red";
            await ctx.Repo.SetStatus(head, Markers.SuiteContext(p), result.ExitCode == 0 ? "success" : "failure", $"{product}'s suite {why} on {ctx.Options.Machine}", ct);
            if (result.ExitCode != 0)
            {
                await ReportRed(product, head, $"main's head, on `{branch}`", why, result.Output, logPath);
                return await CutLastGreen(p, dir, last, StatusMark.Newest(marks, Markers.SuiteContext(p), ok: true)?.Sha, $"suite {why} at {head[..8]}", ct);
            }
        }

        await ctx.Repo.Dispatch(Item.Workflow!, new { tested_sha = head }, ct);
        return new JobOutcome($"suite green at {head[..8]} — {Item.Workflow} dispatched");
    }

    // A suite to run: the worker's command, or its Suite on the repository's builder.
    static bool HasSuite(JobStep step)
    {
        return step.Command is not null || step.Worker.Suite is not null;
    }

    internal static string BranchPrefix(string product)
    {
        return $"release/{product}/";
    }

    // The day's branch, at main's head; the oldest beyond the kept few go.
    async Task<string> OpenBranch(string p, string dir, TextWriter w)
    {
        var branch = $"{BranchPrefix(p)}{DateTimeOffset.UtcNow:yyyyMMdd-HHmm}";
        await Shell.Git(dir, "push", "-q", "origin", $"HEAD:refs/heads/{branch}");
        await w.WriteLineAsync($"opened {branch}");
        foreach (var old in (await Branches(p, dir)).Skip(BranchesKept))
        {
            var gone = await Shell.Run("git", ["push", "-q", "origin", "--delete", old], dir);
            await w.WriteLineAsync(gone.ExitCode == 0 ? $"deleted {old}" : $"could not delete {old}: {gone.Output.Trim()}");
        }
        return branch;
    }

    // The product's release branches on the remote, newest first.
    static async Task<IReadOnlyList<string>> Branches(string p, string dir)
    {
        var refs = await Shell.Git(dir, "for-each-ref", "--sort=-refname", "--format=%(refname:short)", $"refs/remotes/origin/{BranchPrefix(p)}");
        return refs.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()["origin/".Length..]).ToList();
    }

    async Task<ShellResult> Suite(string product, string? filter, string dir, JobStep step, TextWriter w, CancellationToken ct)
    {
        var lease = await ctx.Leases.Acquire(JobContext.HolderOf(Item), Planner.Rank(Item.Tier), ct);
        try
        {
            var env = await ctx.LaneEnv(DateTimeOffset.UtcNow.AddMinutes(step.Minutes), Item, step, ct);
            env["HARNESS_LEASE_HELD"] = "1";
            if (step.Command is { } command)
            {
                string[] args = filter is null ? [product] : [product, filter];
                return await ctx.Run(command, args, dir, env, TimeSpan.FromMinutes(step.Minutes), w, ct);
            }
            var suite = step.Worker.Suite!.Replace("{Product}", product, StringComparison.Ordinal);
            return await ctx.Builder!.Suite(dir, suite, filter, env, TimeSpan.FromMinutes(step.Minutes), w, ct);
        }
        finally
        {
            ctx.Leases.Release(lease.Id);
        }
    }

    // The red story's fix, carried to the newest release branch, and the tests that failed there run
    // again. Null: something it needs is not there, and the cut goes the whole way on main's head.
    async Task<JobOutcome?> FixOnBranch(string p, string product, int red, string dir, JobStep step, TextWriter w, string logPath, CancellationToken ct)
    {
        if ((await Branches(p, dir)).FirstOrDefault() is not { } branch)
        {
            await w.WriteLineAsync($"no {BranchPrefix(p)}* branch — the whole suite on main's head");
            return null;
        }
        // Only the harness's own reds name what a run here must pass: another runner's list is its own.
        var failed = (await ctx.Board.Comments(red, ct)).Where(c => c.Body.Contains(FailedMarker, StringComparison.Ordinal))
            .OrderBy(c => c.At).Select(c => FailedIn(c.Body)).LastOrDefault(f => f.Count > 0);
        if (failed is null)
        {
            await w.WriteLineAsync($"#{red} names no failed tests — the whole suite on main's head");
            return null;
        }

        await Shell.Git(dir, "checkout", "-q", "--detach", $"origin/{branch}");
        var bas = await Shell.Git(dir, "merge-base", "HEAD", "origin/main");
        var picked = await Shell.Git(dir, "log", "--format=%B", $"{bas}..HEAD");
        // The story's merges on main past the branch: a squash names its PR at the end of its subject.
        var prs = (await ctx.Repo.PrsOf(red, "merged", ct)).Select(pr => pr.Number).ToList();
        var fixes = FixesIn(await Shell.Git(dir, "log", "--reverse", "--format=%H %s", $"{bas}..origin/main"), prs)
            .Where(s => !picked.Contains($"cherry picked from commit {s}", StringComparison.Ordinal)).ToList();
        if (fixes.Count == 0)
        {
            await w.WriteLineAsync($"no commit of #{red} on main past {branch} — the whole suite on main's head");
            await Shell.Git(dir, "checkout", "-q", "--detach", "origin/main");
            return null;
        }
        foreach (var fix in fixes)
        {
            var pick = await Shell.Run("git", ["-c", $"user.name={ctx.Options.CommitName}", "-c", $"user.email={ctx.Options.CommitEmail}", "cherry-pick", "-x", fix], dir);
            if (pick.ExitCode == 0) continue;
            await Shell.Run("git", ["cherry-pick", "--abort"], dir);
            await w.WriteLineAsync($"the fix {fix[..8]} does not apply on {branch}: {pick.Output.Trim()}");
            await Shell.Git(dir, "checkout", "-q", "--detach", "origin/main");
            await w.WriteLineAsync("the whole suite on main's head instead");
            return null;
        }
        var tip = await Shell.Git(dir, "rev-parse", "HEAD");
        await Shell.Git(dir, "push", "-q", "origin", $"HEAD:refs/heads/{branch}");
        await w.WriteLineAsync($"carried {string.Join(", ", fixes.Select(f => f[..8]))} to {branch} at {tip[..8]}; again: {string.Join(", ", failed)}");

        var result = await Suite(product, Filter(failed), dir, step, w, ct);
        // A filter that matches nothing (the tests were renamed or removed) passes on nothing.
        if (result.Output.Contains("No test matches the given testcase filter", StringComparison.Ordinal))
        {
            await w.WriteLineAsync("the failed tests are gone by those names — the whole suite on main's head");
            await Shell.Git(dir, "checkout", "-q", "--detach", "origin/main");
            return null;
        }
        var green = result.ExitCode == 0;
        await ctx.Repo.SetStatus(tip, Markers.SuiteContext(p), green ? "success" : "failure", $"{product}'s failed tests {(green ? "green" : "red")} again on {branch}, on {ctx.Options.Machine}", ct);
        if (!green)
        {
            await ReportRed(product, tip, $"`{branch}`, with the fix", result.TimedOut ? $"passed {step.Minutes} min" : "red", result.Output, logPath);
            return new JobOutcome($"the fix of #{red} is still red on {branch}", Failed: true);
        }
        await ctx.Repo.Dispatch(Item.Workflow!, new { tested_sha = tip }, ct);
        return new JobOutcome($"the fix of #{red} green on {branch} at {tip[..8]} — {Item.Workflow} dispatched");
    }

    async Task<JobOutcome> CutLastGreen(string p, string dir, string? last, string? green, string summary, CancellationToken ct)
    {
        if (green is null) return new JobOutcome($"{summary}; no green sha to release", Failed: true);
        if (last is not null)
        {
            var held = await Shell.Run("git", ["merge-base", "--is-ancestor", green, last], dir);
            if (held.ExitCode == 0) return new JobOutcome($"{summary}; the last green ({green[..8]}) is already in {last}", Failed: true);
        }
        await ctx.Repo.Dispatch(Item.Workflow!, new { tested_sha = green }, ct);
        return new JobOutcome($"{summary}; {Item.Workflow} dispatched on the last green, {green[..8]}", Failed: true);
    }

    // One story per product: a new red comments on the open one (and sends it back to Ready when it
    // had been judged fixed); with none open, a new one is born with the red labels and Ready. The
    // comment carries the failed tests' names, which the fix's run reads back.
    async Task ReportRed(string product, string sha, string where, string why, string output, string logPath)
    {
        var failed = GateJob.Failures(output);
        var title = HarnessTexts.Fill(ctx.Texts.SuiteRedTitle, ("product", product));
        var body = HarnessTexts.Fill(ctx.Texts.SuiteRed, ("product", product), ("why", why), ("sha", sha), ("where", where), ("failed", failed), ("log", logPath), ("machine", ctx.Options.Machine))
            + $"\n\n{ctx.Texts.Signature}";
        if (FailedIn(failed) is { Count: > 0 } names) body += $"\n{FailedMarker} {string.Join('|', names)} -->";
        if (await ctx.Board.FindOpen(title) is { } n)
        {
            await ctx.Board.Comment(n, body);
            var stage = await ctx.Board.StageOf(n);
            if (Stages.Landed.Contains(stage)) await ctx.Board.SetStage(n, Stages.Ready);
            return;
        }
        var created = await ctx.Board.CreateStory(title, body);
        await ctx.Board.AddLabels(created, ctx.Options.RedLabels.ToArray());
        await ctx.Board.SetStage(created, Stages.Ready);
    }

    internal const string FailedMarker = "<!-- harness:failed";

    // The failed tests a text names: the harness's own marker when it carries one, else the runner's
    // `Failed <name>` lines; a theory's arguments are dropped, its name runs every case.
    internal static IReadOnlyList<string> FailedIn(string text)
    {
        var marker = text.IndexOf(FailedMarker, StringComparison.Ordinal);
        if (marker >= 0)
        {
            var rest = text[(marker + FailedMarker.Length)..];
            var end = rest.IndexOf("-->", StringComparison.Ordinal);
            return (end < 0 ? rest : rest[..end]).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        }
        return FailedLine().Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    // From `git log --format="%H %s"`, the commits whose subject ends naming one of these PRs.
    internal static IReadOnlyList<string> FixesIn(string log, IReadOnlyCollection<int> prs)
    {
        return log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim().Split(' ', 2))
            .Where(p => p.Length == 2 && SquashOf().Match(p[1]) is { Success: true } m && prs.Contains(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .Select(p => p[0]).ToList();
    }

    internal static string Filter(IReadOnlyList<string> names)
    {
        return string.Join('|', names.Select(n => $"FullyQualifiedName~{n}"));
    }

    [GeneratedRegex(@"^\s*Failed ([\w.]+)", RegexOptions.Multiline)] private static partial Regex FailedLine();
    [GeneratedRegex(@"\(#(\d+)\)$")] private static partial Regex SquashOf();
}
