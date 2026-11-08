// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Harness.Agents;
using NSail.Harness.Board;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness.Jobs;

// One builder per story, up to an open PR: the story is named by the planner, the harness lays the
// round's branch and the story's dossier in the slot, the builder codes, builds and commits, and
// leaves its ending in .voyage/out/ — a PR's words, a ruling, a hand-off. The builder holds no
// token and pushes nothing: the harness pushes what it committed and acts on its ending, and the
// gate decides what follows.
public sealed class DevJob(WorkItem item, JobContext ctx, JobHistory history) : IJob
{
    const string Voyage = ".voyage";

    public WorkItem Item { get; } = item;

    JobStep Step => ctx.StepOf(Item);

    AgentOptions Seat => Step.Agent!;

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var n = Item.Story!.Value;
        var board = ctx.Board;
        var log = ctx.Logs.CreateLogger($"dev#{n}");

        var (title, body, open) = await board.Story(n, ct);
        if (!open) return new JobOutcome("closed — nothing to build", Idle: true);
        if (await board.Children(n, ct) > 0) return new JobOutcome("an epic — a spine is built through its children", Idle: true);

        // A name overrules holds, never the merge: a landed story out of Ready has nothing to build.
        var stage = await board.StageOf(n, ct);
        if (stage != Stages.Ready && await Landed(n, null, ct))
        {
            await board.RemoveLabel(n, Labels.Claimed);
            await board.TrySetStage(n, Stages.Merged, log, ct);
            return new JobOutcome("already landed", Idle: true);
        }

        var labels = await board.Labels(n, ct);
        var model = ctx.ModelOf(labels, Seat);

        // A new voyage on a story whose body or refinery comment names a blocker in words waits for it as
        // if the board linked it: the link is made, and the story held while the blocker is open.
        if (Item.Pr is null)
        {
            var waiting = await WrittenBlockers.Link(board, n, $"{body}\n{await RefineryComment(n, ct)}", ctx.Names.Key, ct);
            if (waiting.Count > 0)
            {
                await board.RemoveLabel(n, Labels.Claimed);
                return new JobOutcome($"waits on {string.Join(", ", waiting.Select(w => $"#{w}"))} — linked and blocked", Idle: true);
            }
        }

        var tries = await DeadVoyages(n, ct);
        if (tries >= 2)
        {
            await board.AddLabels(n, Labels.Stalled);
            await board.RemoveLabel(n, Labels.Claimed);
            await board.Comment(n, Said(HarnessTexts.Fill(ctx.Texts.TwoDeadVoyages, ("machine", ctx.Options.Machine))));
            await board.TrySetStage(n, Stages.Analyze, log, ct);
            return new JobOutcome("two dead voyages — stalled", Idle: true);
        }

        // The stage first, the lock after: a board that guards moves refuses one under a lock.
        await board.RemoveLabel(n, Labels.Review);
        await board.TrySetStage(n, Stages.InProgress, log, ct);
        if (!labels.Contains(Labels.Claimed)) await board.AddLabels(n, Labels.Claimed);
        var claimedAt = DateTimeOffset.UtcNow;
        var axe = claimedAt.AddMinutes(Step.Minutes - 5);

        // The default repository's slot carries the prompt and the plugins; the round builds in the
        // repository its story lives in, in that repository's own checkout.
        var dir = await ctx.Workspace.Prepare(slot);
        // The prompt and the doctrine are main's, never the story branch's: a branch cut before a change
        // to them would hand the seat words for a world that no longer exists.
        var doctrine = await ctx.Workspace.PrepareDoctrine(slot);
        var repo = await RepositoryOf(n, labels, ct);
        var home = repo == ctx.Repo;
        var work = home ? dir : await ctx.WorkspaceOf(repo).Prepare(slot);
        var branch = Item.Pr is { } round ? (await repo.Pr(round, ct)).HeadRef : ctx.Names.Branch(ctx.Repos.OptionsOf(repo).BranchPrefix, n);
        var startedAt = await Checkout(work, branch, ctx.WorkspaceOf(repo).Tip);
        await Exclude(work, Voyage);
        var voyage = Path.Combine(work, Voyage);
        if (Directory.Exists(voyage)) Directory.Delete(voyage, recursive: true);
        Directory.CreateDirectory(Path.Combine(voyage, "out"));
        var dossier = new StoryDossier(ctx, history);
        await dossier.WriteStory(n, voyage, ct);
        await dossier.WritePrs(n, voyage, ct);
        var env = await ctx.LaneEnv(axe, Item, Step, ct);
        await using (var w = new StreamWriter(logPath, append: true) { AutoFlush = true })
        {
            await w.WriteLineAsync($"== {ctx.Names.Ref(n)} {title} — {model} on {ctx.Options.Machine} slot {slot}, voyage {tries + 1}");
            await ctx.Prepare(doctrine, env, w);
            if (home) await ctx.Prepare(work, env, w);
        }

        var prompt = await SeatPrompt.Read(doctrine, Seat, new Dictionary<string, string>
        {
            ["ISSUE"] = n.ToString(CultureInfo.InvariantCulture),
            ["REPO"] = ctx.Repo.Name,
            ["AXE_AT"] = $"{axe:HH:mm} UTC",
            ["BRANCH"] = branch,
        }, ct);

        AgentResult result;
        try
        {
            result = await ctx.RunSeat(new AgentRun(
                prompt, work, model,
                Access: AgentAccess.Builder,
                MaxTurns: 300,
                Timeout: TimeSpan.FromMinutes(Step.Minutes),
                Env: ctx.SeatEnv(env),
                LogPath: logPath,
                Marketplace: doctrine), ct);
        }
        catch
        {
            // A cancelled or crashed run never reaches Deliver: the slot's next Prepare resets it.
            try
            {
                await Rescue(n, repo, work, startedAt ?? ctx.WorkspaceOf(repo).Tip, log);
            }
            catch (Exception e)
            {
                log.LogError(e, "#{Story}: the rescue push failed", n);
            }
            throw;
        }

        var ending = await Deliver(n, title, repo, work, branch, startedAt, claimedAt, result, log, ct);
        return ending.Outcome(result.QuotaRefused ? result.QuotaUntil ?? DateTimeOffset.UtcNow.AddHours(1) : null);
    }

    // The analyst's last comment on the story: the one signed by a seat an Analyze worker sits.
    async Task<string> RefineryComment(int n, CancellationToken ct)
    {
        var signatures = ctx.Options.Workers.Values.Where(w => w.Type == "Analyze" && w.Seat is not null)
            .Select(w => ctx.Options.Seats.TryGetValue(w.Seat!, out var s) ? s.Signature : "").Where(s => s.Length > 0).ToList();
        if (signatures.Count == 0) return "";
        return (await ctx.Board.Comments(n, ct)).LastOrDefault(c => signatures.Any(s => c.Body.Contains(s, StringComparison.Ordinal)))?.Body ?? "";
    }

    // What the harness says about a voyage on the story: marked, so the next voyage can count them.
    string Said(string text)
    {
        return $"{text} {ctx.Texts.Signature}\n{Markers.Voyage}";
    }

    // A round of a PR builds where the PR lives. A story with open PRs all in one repository builds
    // there; a new one where its area labels route it (RepositorySet.RoutedBy), a two-half story's
    // routed half first.
    async Task<IRepository> RepositoryOf(int n, IReadOnlySet<string> labels, CancellationToken ct)
    {
        if (Item.PrRepo is { } r) return ctx.Repos.Of(r);
        var open = (await ctx.Repos.PrsOf(n, "open", ct)).Select(p => p.Repo).Distinct().ToList();
        if (open.Count == 1) return ctx.Repos.Of(open[0]);
        return open.Count > 1 ? ctx.Repo : await ctx.Repos.RoutedBy(n, labels, ct);
    }

    // The dossier never rides a commit: the clone's own exclude list hides it, in whatever repository.
    static async Task Exclude(string dir, string path)
    {
        var common = (await Shell.Git(dir, "rev-parse", "--git-common-dir")).Trim();
        var exclude = Path.Combine(Path.IsPathRooted(common) ? common : Path.Combine(dir, common), "info", "exclude");
        Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);
        var line = $"/{path}/";
        if (!File.Exists(exclude) || !File.ReadAllLines(exclude).Contains(line)) await File.AppendAllTextAsync(exclude, line + Environment.NewLine);
    }

    // The branch as the remote holds it, or a new one off the default branch. A slot an earlier job left on it does
    // not hold it: the story's lock already says only one job builds it at a time. Returns the remote head the
    // voyage starts from, null for a new branch: what its push expects to replace.
    static async Task<string?> Checkout(string dir, string branch, string tip)
    {
        var remote = await Shell.Run("git", ["rev-parse", "-q", "--verify", $"refs/remotes/origin/{branch}"], dir);
        var from = remote.ExitCode == 0 ? remote.Output.Trim() : tip;
        await Shell.Git(dir, "checkout", "-q", "--ignore-other-worktrees", "-B", branch, from);
        return remote.ExitCode == 0 ? from : null;
    }

    // Commits ahead of `since` go to a ref of their own, unleased and never the story's: a push that must
    // survive a cancelled job or a refused lease. Returns the ref, null when nothing was ahead.
    async Task<string?> Rescue(int n, IRepository repo, string dir, string since, ILogger log)
    {
        var name = $"rescue/{ctx.Names.Key}-{n}";
        var rescued = await RescueRef(repo, dir, since, name);
        if (rescued is not null) log.LogWarning("#{Story}: unpushed work rescued to {Ref}", n, rescued);
        return rescued;
    }

    internal static async Task<string?> RescueRef(IRepository repo, string dir, string since, string name)
    {
        var ahead = int.Parse((await Shell.Git(dir, "rev-list", "--count", $"{since}..HEAD")).Trim(), CultureInfo.InvariantCulture);
        if (ahead == 0) return null;
        for (var i = 0; ; i++)
        {
            var candidate = i == 0 ? name : $"{name}-{i + 1}";
            try
            {
                await repo.Push(dir, candidate, null, CancellationToken.None);
                return candidate;
            }
            catch (InvalidOperationException) when (i < 20)
            {
            }
        }
    }

    // What the builder committed goes up first, unless it touches a protected path: work is never lost to the axe.
    // Then its ending, exactly one: a PR's words, a ruling, a hand-off; none is a dead voyage.
    async Task<Ending> Deliver(int n, string title, IRepository repo, string dir, string branch, string? startedAt, DateTimeOffset claimedAt, AgentResult result, ILogger log, CancellationToken ct)
    {
        var board = ctx.Board;
        string Out(string file)
        {
            var path = Path.Combine(dir, Voyage, "out", file);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }

        var remote = await Shell.Run("git", ["rev-parse", "-q", "--verify", $"refs/remotes/origin/{branch}"], dir);
        var since = startedAt ?? ctx.WorkspaceOf(repo).Tip;
        var ahead = int.Parse((await Shell.Git(dir, "rev-list", "--count", $"{since}..HEAD")).Trim(), CultureInfo.InvariantCulture);
        var pushed = false;
        if (ahead > 0)
        {
            var touched = ctx.Options.AllowProtectedPaths || (await board.Labels(n, ct)).Contains(Labels.AllowProtected)
                ? []
                : SeatOutput.Protected(await SeatOutput.Changed(dir, since), ctx.Options.ProtectedPaths);
            if (touched.Count > 0)
            {
                await board.RemoveLabel(n, Labels.Claimed);
                await board.Comment(n, Said(HarnessTexts.Fill(ctx.Texts.ProtectedPaths, ("paths", string.Join(", ", touched.Select(f => $"`{f}`"))), ("label", Labels.AllowProtected))));
                if ((await ctx.Repos.PrsOf(n, "open", ct)).Count == 0) await board.TrySetStage(n, Stages.Ready, log, ct);
                return new Ending("push refused — protected paths touched", Failed: true);
            }
            try
            {
                await repo.Push(dir, branch, startedAt, ct);
                pushed = true;
            }
            catch (InvalidOperationException refused)
            {
                log.LogWarning("#{Story}: {Refused}", n, refused.Message);
                var rescue = await Rescue(n, repo, dir, since, log);
                await board.RemoveLabel(n, Labels.Claimed);
                await board.Comment(n, Said(HarnessTexts.Fill(ctx.Texts.PushRefused, ("branch", branch), ("rescue", rescue))));
                if ((await ctx.Repos.PrsOf(n, "open", ct)).Count == 0) await board.TrySetStage(n, Stages.Ready, log, ct);
                return new Ending($"push refused — work on {rescue}", Failed: true);
            }
        }

        await board.RemoveLabel(n, Labels.Claimed);

        if (Out("close-pr.md") is { Length: > 0 } close)
        {
            var lines = close.Split('\n', 2);
            if (int.TryParse(lines[0].Trim().TrimStart('#'), out var dead))
            {
                if (!(await repo.PrsOf(n, "open", ct)).Any(p => p.Number == dead))
                {
                    await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.NotOwnPr, ("pr", dead))} {ctx.Texts.Signature}");
                }
                else await repo.ClosePr(dead, JobContext.Signed(lines.Length > 1 ? lines[1].Trim() : ctx.Texts.CannotContinue, Seat), ct);
            }
        }

        if (result.QuotaRefused)
        {
            // The usage wall is no story's fault: it does not count as a dead voyage.
            await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.QuotaWall, ("until", $"{result.QuotaUntil:HH:mm}"))} {ctx.Texts.Signature}");
            if ((await ctx.Repos.PrsOf(n, "open", ct)).Count == 0) await board.TrySetStage(n, Stages.Ready, log, ct);
            return "quota";
        }

        // A builder that adds nothing to a story whose merge is already on main has nothing to build, whatever
        // else it says: its words go up, the harness says what it read, and the story lands instead of
        // going round again (a hand-off sends it to Ready, a ruling to the analyst, who sends it back).
        async Task<Ending?> NothingToBuild(string words)
        {
            if (pushed) return null;
            var evidence = await Landing.NothingToBuild(ctx.Repos, board, n, await board.Labels(n, ct), WorkerTypes.QaBugMarker, ct);
            if (evidence is null) return null;
            await board.Comment(n, JobContext.Signed(words, Seat));
            await board.Comment(n, $"{HarnessTexts.Fill(ctx.Texts.LandedNothingToBuild, ("evidence", evidence))} {ctx.Texts.Signature}");
            log.LogInformation("#{Story}: nothing left to build — {Evidence}", n, evidence);
            return new Ending($"nothing left to build — {evidence}", "Landed");
        }

        if (Out("ruling.md") is { Length: > 0 } question)
        {
            if (await NothingToBuild(question) is { } landed) return landed;
            await board.Comment(n, JobContext.Signed(question, Seat));
            return new Ending("ruling", "Ruling");
        }

        if (Out("handoff.md") is { Length: > 0 } handoff)
        {
            if (await NothingToBuild(handoff) is { } landed) return landed;
            await board.Comment(n, JobContext.Signed(handoff, Seat));
            return new Ending(pushed ? "handed off, work pushed" : "handed off", "HandedOff");
        }

        if (Out("pr.md") is { Length: > 0 } words && (pushed || remote.ExitCode == 0))
        {
            var open = (await repo.PrsOf(n, "open", ct)).FirstOrDefault(p => p.Head == branch);
            if (open is null)
            {
                var lines = ctx.Markers.Strip(words).Split('\n', 2);
                var prTitle = ctx.Names.Untitled(lines[0]);
                var story = HarnessTexts.Fill(ctx.Texts.PrStory, ("story", $"{ctx.Repo.Name}#{n}"));
                var number = await repo.CreatePr(branch, ctx.Names.PrTitle(n, prTitle.Length > 0 ? prTitle : title),
                    $"{(lines.Length > 1 ? lines[1].Trim() : "")}\n\n{story}".TrimStart(), ct);
                return new Ending($"PR #{number} opened; the gate decides", "Opened");
            }
            if (!pushed)
            {
                await board.Comment(n, Said(HarnessTexts.Fill(ctx.Texts.NothingPushed, ("pr", open.Number))));
                return new Ending($"PR #{open.Number} open, nothing pushed — dead", Failed: true);
            }
            await repo.Comment(open.Number, JobContext.Signed(ctx.Markers.Strip(words), Seat));
            return new Ending($"PR #{open.Number} updated; the gate decides", "Pushed");
        }

        if (await Landed(n, claimedAt, ct))
        {
            return new Ending("landed while the lane sailed", "Landed");
        }

        var why = result.TimedOut ? "timeout" : $"exit {result.ExitCode}";
        await board.Comment(n, Said(HarnessTexts.Fill(ctx.Texts.DeadVoyage, ("why", why), ("pushed", pushed ? ctx.Texts.WorkPushed : ""))));
        if ((await ctx.Repos.PrsOf(n, "open", ct)).Count == 0) await board.TrySetStage(n, Stages.Ready, log, ct);
        return new Ending($"dead voyage ({why})", Failed: true);
    }

    // The story rides main: nothing open on its branches and a merge (since `since`, if given) in the
    // repository it is built in — a Stack story's merge is the Stack's, not the default repository's.
    async Task<bool> Landed(int n, DateTimeOffset? since, CancellationToken ct)
    {
        if ((await ctx.Repos.PrsOf(n, "open", ct)).Count > 0) return false;
        var repo = await ctx.Repos.RoutedBy(n, await ctx.Board.Labels(n, ct), ct);
        return (await repo.PrsOf(n, "merged", ct)).Any(p => since is null || p.MergedAt >= since);
    }

    // Dead voyages since a hand last lifted a hold: a span between two voyage comments counts only if
    // no branch of the story moved inside it — progress forgives.
    async Task<int> DeadVoyages(int n, CancellationToken ct)
    {
        var since = await ctx.Board.LastLift(n, Labels.Lifts, ct);
        var said = (await ctx.Board.Comments(n, ct))
            .Where(c => c.Body.Contains(Markers.Voyage, StringComparison.Ordinal) && (since is null || c.At > since))
            .Select(c => c.At).OrderBy(t => t).ToList();
        var pushes = await ctx.Repo.PushTimes(n, ct);
        return Count(said, pushes, since);
    }

    internal static int Count(IReadOnlyList<DateTimeOffset> said, IReadOnlyList<DateTimeOffset> pushes, DateTimeOffset? since)
    {
        var tries = 0;
        var prev = since ?? DateTimeOffset.MinValue;
        foreach (var at in said)
        {
            tries = pushes.Any(p => p > prev && p < at) ? 0 : tries + 1;
            prev = at;
        }
        return tries;
    }
}
