// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Harness.Board;
using NSail.Harness.Repositories;

namespace NSail.Harness.Jobs;

// When a story's code already rides main, and the harness can say so from the repositories and the
// board instead of sending a builder to find out again. A seat's "nothing left to build" is a claim:
// this is what makes it a fact. It is a rule over evidence, not a watcher — it runs where a job
// ends (a builder's hand-off or ruling with nothing pushed, a green main gate) and says out loud,
// on the story, what it read.
public static class Landing
{
    // The story's merge in the repository it is built in, and nothing else of it left: no PR open in
    // any repository, no branch of it ahead of main, and no QA bug said after that merge (a bug sends
    // a story back for new work, whatever its old merge). The evidence in words, or null.
    public static string? Evidence(IReadOnlyList<PrRef> open, IReadOnlyList<PrRef> merged, int aheadOfMain, IReadOnlyList<StoryComment> comments, string bugMarker)
    {
        if (open.Count > 0 || aheadOfMain > 0 || merged.Count == 0) return null;
        var last = merged.Where(p => p.MergedAt is not null).OrderBy(p => p.MergedAt).LastOrDefault();
        if (last is null) return null;
        if (comments.Any(c => c.At > last.MergedAt && c.Body.Contains(bugMarker, StringComparison.Ordinal))) return null;
        return $"{last.Repo}#{last.Number} merged {last.MergedAt:yyyy-MM-dd HH:mm} UTC, no PR open and nothing of its branches ahead of main";
    }

    // A red main's fix: its merge, with no PR of the story open, and the head main's gate just
    // judged green holds that merge — so the gate has run the range that held the red and the fix
    // (it diffs from the last head it found green, which no red head is).
    public static string? Fixed(IReadOnlyList<PrRef> open, Merge? merge, bool headHoldsMerge, string head)
    {
        if (open.Count > 0 || merge is null || !headHoldsMerge) return null;
        return string.Create(CultureInfo.InvariantCulture, $"the fix merged as `{merge.Sha[..Math.Min(8, merge.Sha.Length)]}` ({merge.At:yyyy-MM-dd HH:mm} UTC) and main's gate is green at `{head[..Math.Min(8, head.Length)]}`, which holds it");
    }

    // Reads the story's evidence from where it lives. The merge counts in the repository the story's
    // own labels route it to: a Stack story rides the Stack's main, not the default repository's.
    public static async Task<string?> NothingToBuild(RepositorySet repos, IBoard board, int story, IReadOnlySet<string> labels, string bugMarker, CancellationToken ct)
    {
        var open = await repos.PrsOf(story, "open", ct);
        if (open.Count > 0) return null;
        var repo = await repos.RoutedBy(story, labels, ct);
        var merged = await repo.PrsOf(story, "merged", ct);
        if (merged.Count == 0) return null;
        var ahead = (await repo.PushTimes(story, ct)).Count;
        return Evidence(open, merged, ahead, await board.Comments(story, ct), bugMarker);
    }
}
