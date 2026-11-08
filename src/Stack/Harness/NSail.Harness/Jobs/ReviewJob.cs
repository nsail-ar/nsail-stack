// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Harness.Agents;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Jobs;

// The reviewer, cold, bound to the PR and never to whoever built it. It reports; the rules decide:
//   clean + CI green + head unchanged -> land
//   findings -> another round, at most three; findings on the fourth read -> `stalled`
//   ruling   -> a crack in the story's logic: back to Analyze
//   no verdict -> `stalled`, loud, never a silent retry
// From the third read on a read is NARROW: the delta since the last read, whether the earlier
// findings closed, what the merge with main broke, and any defect in what the story delivers.
public sealed class ReviewJob(WorkItem item, JobContext ctx, JobHistory history) : IJob
{
    const string FindingsFile = ".review.md";

    public WorkItem Item { get; } = item;

    JobStep Step => ctx.StepOf(Item);

    AgentOptions Seat => Step.Agent!;

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var prNumber = Item.Pr!.Value;
        var board = ctx.Board;
        var log = ctx.Logs.CreateLogger($"review#{prNumber}");

        var pr = await ctx.Repo.Pr(prNumber, ct);
        if (!pr.Open) return new JobOutcome($"PR #{prNumber} is not open", Idle: true);
        var head = pr.HeadRef;
        var sha = pr.HeadSha;
        var n = ctx.Names.StoryOf(pr.Title, head);

        if (n is { } s0) await board.AddLabels(s0, Labels.Review);

        var started = DateTimeOffset.UtcNow;
        var doctrine = await ctx.Workspace.PrepareDoctrine(slot);
        var dir = await ctx.Workspace.Prepare(slot);
        await Shell.Git(dir, "checkout", "-q", "--detach", "-f", sha);

        var (reads, last) = await PriorReads(prNumber, dir, ct);

        // No review on a red or unsettled head: the story goes back to the queue.
        var ci = await CiAt(head, sha, pr, ct);
        if (ci != CiState.Green)
        {
            if (n is { } s1)
            {
                await board.RemoveLabel(s1, Labels.Review);
                await board.Comment(s1, JobContext.Signed(HarnessTexts.Fill(ctx.Texts.ReviewNotGreen, ("pr", prNumber), ("ci", ci)), Seat));
            }
            return new JobOutcome($"CI {ci} — not reviewed", Idle: true);
        }

        var env = await ctx.LaneEnv(started.AddMinutes(Step.Minutes), Item, Step, ct);
        await using (var w = new StreamWriter(logPath, append: true) { AutoFlush = true })
        {
            await w.WriteLineAsync($"== review PR #{prNumber} at {sha[..8]} (story {n?.ToString(CultureInfo.InvariantCulture) ?? "-"}, reads {reads}) on {ctx.Options.Machine} slot {slot}");
            await ctx.Prepare(doctrine, env, w);
        }
        File.Delete(Path.Combine(dir, FindingsFile));

        // What the board and the repository hold about the story and its PRs, on disk: the seat reads it
        // there and holds no token.
        var voyage = Path.Combine(dir, ".voyage");
        if (Directory.Exists(voyage)) Directory.Delete(voyage, recursive: true);
        Directory.CreateDirectory(voyage);
        var dossier = new StoryDossier(ctx, history);
        if (n is { } sd)
        {
            await dossier.WriteStory(sd, voyage, ct);
            await dossier.WritePrs(sd, voyage, ct);
        }

        var result = await ctx.RunSeat(new AgentRun(
            await Prompt(doctrine, prNumber, n, reads, last, ct),
            dir, Seat.Model,
            Access: AgentAccess.Reader,
            MaxTurns: 200,
            Timeout: TimeSpan.FromMinutes(Step.Minutes),
            Env: ctx.SeatEnv(env),
            LogPath: logPath,
            Marketplace: doctrine), ct);

        var ending = await Decide(prNumber, n, sha, head, Path.Combine(dir, FindingsFile), result, log, ct);
        return ending.Outcome(result.QuotaRefused ? result.QuotaUntil ?? DateTimeOffset.UtcNow.AddHours(1) : null);
    }

    async Task<string> Prompt(string doctrine, int pr, int? story, int reads, string? last, CancellationToken ct)
    {
        var text = await SeatPrompt.Read(doctrine, Seat, new Dictionary<string, string>
        {
            ["PR"] = pr.ToString(CultureInfo.InvariantCulture),
            ["REPO"] = ctx.Repo.Name,
            ["FINDINGS"] = FindingsFile,
            ["AGAINST"] = story is { } s ? $"against its story, {ctx.Names.Ref(s)} (`.voyage/story.md`)" : "against its own description",
        }, ct);
        if (reads >= 2 && (Seat.NarrowPrompt ?? Seat.NarrowPromptFile) is not null)
        {
            var narrow = await SeatPrompt.Read(doctrine, Seat.NarrowPrompt, Seat.NarrowPromptFile, new Dictionary<string, string>
            {
                ["READS"] = reads.ToString(CultureInfo.InvariantCulture),
                ["LAST"] = last ?? "",
            }, ct);
            text = $"{text.TrimEnd()}\n\n{narrow}";
        }
        return text;
    }

    async Task<Ending> Decide(int prNumber, int? n, string sha, string head, string findings, AgentResult result, ILogger log, CancellationToken ct)
    {
        var board = ctx.Board;
        var text = File.Exists(findings) ? await File.ReadAllTextAsync(findings, ct) : "";
        var first = text.Split('\n').FirstOrDefault()?.Trim() ?? "";
        var kind = first switch
        {
            "VERDICT: clean" => "clean",
            "VERDICT: findings" => "findings",
            "VERDICT: ruling" => "ruling",
            _ => null,
        };

        if (kind is null)
        {
            if (result.QuotaRefused)
            {
                if (n is { } q) await board.RemoveLabel(q, Labels.Review);
                return "quota — no verdict, story back in the queue";
            }
            if (n is { } s) await Stall(s, HarnessTexts.Fill(ctx.Texts.ReviewNoVerdict, ("pr", prNumber)), log, ct);
            return new Ending("no verdict — stalled", Failed: true);
        }

        // The marker counts rounds; the body is what people read.
        var body = ctx.Markers.Strip(string.Join('\n', text.Split('\n').Skip(1))).TrimEnd() + "\n\n" + ctx.Markers.Review(kind, sha);
        await ctx.Repo.Comment(prNumber, body);

        // What a narrow read saw out of its scope stays in that comment, on the record: it is not
        // filed as a story of its own, which bred follow-ups faster than the queue closed them.
        if (n is not { } story) return $"{kind} — PR is no story's, reviewed only";

        if (kind == "ruling")
        {
            await board.RemoveLabel(story, Labels.Review);
            await board.Comment(story, $"{HarnessTexts.Fill(ctx.Texts.ReviewStoryCrack, ("pr", prNumber), ("branch", head))}\n\n{body}");
            return new Ending("ruling — back to Analyze", "Ruling");
        }

        if (kind == "findings")
        {
            // Counted from the last time a hand lifted a hold: a story sent back gets its rounds anew,
            // or every rescue would stall again on its first read.
            var since = await board.LastLift(story, Labels.Lifts, ct);
            var rounds = ctx.Markers.Findings(ctx.Markers.Bodies((await ctx.Repo.Thread(prNumber, ct)).Where(b => since is null || b.At > since), await ctx.Repo.Login(ct))).Count;
            if (rounds >= 4)
            {
                await Stall(story, HarnessTexts.Fill(ctx.Texts.ReviewFourthRead, ("pr", prNumber)), log, ct);
                return new Ending("fourth read with findings — stalled", Failed: true);
            }
            await board.RemoveLabel(story, Labels.Review);
            // Ready with findings at this head (the Findings event): the planner sends it to a round.
            return new Ending($"findings — round {rounds}", "Findings");
        }

        var now = await ctx.Repo.Pr(prNumber, ct);
        await board.RemoveLabel(story, Labels.Review);
        if (now.HeadSha != sha) return "clean, but the head moved during the read";
        return await Land(prNumber, sha, story, HarnessTexts.Fill(ctx.Texts.Merged, ("pr", prNumber), ("title", now.Title)), log, ct);
    }

    // The whole green ending: merge, then say what landed; the move to Merged and the claim's release
    // are the Clean event's (WorkerTypes). A story with a half still open is StillOpen instead — back
    // to Ready: it rides main only when nothing of it is left open.
    async Task<Ending> Land(int pr, string sha, int story, string what, ILogger log, CancellationToken ct)
    {
        var board = ctx.Board;
        try
        {
            await ctx.Repo.Merge(pr, sha, ct);
        }
        catch (ProviderException e)
        {
            log.LogWarning("PR #{Pr} did not merge: {Error}", pr, e.Message);
            await board.Comment(story, JobContext.Signed(HarnessTexts.Fill(ctx.Texts.MergeRefused, ("pr", pr)), Seat));
            await board.TrySetStage(story, Stages.Ready, log, ct);
            return new Ending("clean, merge refused — back to Ready", Failed: true);
        }

        var rest = (await ctx.Repos.PrsOf(story, "open", ct)).Where(p => !(p.Number == pr && p.Repo == ctx.Repo.Name)).ToList();
        if (rest.Count > 0)
        {
            var list = string.Join(", ", rest.Select(p => $"#{p.Number}"));
            await board.Comment(story, JobContext.Signed($"{what}\n\n{HarnessTexts.Fill(ctx.Texts.StillOpen, ("prs", list))}", Seat));
            return new Ending($"merged, {list} still open — Ready", "StillOpen");
        }

        await board.Comment(story, JobContext.Signed(what, Seat));
        return new Ending("landed", "Clean");
    }

    async Task Stall(int story, string why, ILogger log, CancellationToken ct)
    {
        await ctx.Board.AddLabels(story, Labels.Stalled);
        await ctx.Board.RemoveLabel(story, Labels.Review);
        await ctx.Board.TrySetStage(story, Stages.Analyze, log, ct);
        await ctx.Board.Comment(story, JobContext.Signed(why, Seat));
    }

    // Earlier reads with findings, oldest first; the last one's sha is where a narrow read starts.
    async Task<(int Reads, string? Last)> PriorReads(int pr, string dir, CancellationToken ct)
    {
        var shas = ctx.Markers.Findings(ctx.Markers.Bodies(await ctx.Repo.Thread(pr, ct), await ctx.Repo.Login(ct)));
        var last = shas.LastOrDefault();
        if (last is not null && (await Shell.Run("git", ["cat-file", "-e", $"{last}^{{commit}}"], dir)).ExitCode != 0) last = null;
        return (shas.Count, last);
    }

    async Task<CiState> CiAt(string branch, string sha, PullRequest pr, CancellationToken ct)
    {
        if (pr.Conflicting) return CiState.Conflict;
        if (ctx.Options.Gate)
        {
            var bodies = ctx.Markers.Bodies(await ctx.Repo.Thread(Item.Pr!.Value, ct), await ctx.Repo.Login(ct));
            return Markers.GateAt(bodies, sha) ?? CiState.Pending;
        }
        return await ctx.Repo.Ci(branch, sha, ct);
    }
}
