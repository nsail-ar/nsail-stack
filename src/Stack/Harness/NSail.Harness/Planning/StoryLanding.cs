// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Repositories;

namespace NSail.Harness.Planning;

// Where a story's work reached the default branch, for every reader that asks whether a fix landed
// or shipped: the story's own newest merge (RepositorySet.LandingOf), or the commit its analysis
// named as already holding it (Markers.FixOf), whichever is newer.
// A named fix is dated by the analysis's word, never by the commit: "shipped" is read by time, and a
// red cut publishes the last green right after it fails, so a release published between the fix's
// merge and the analysis would read as shipping a fix it may not hold.
public static class StoryLanding
{
    public static async Task<Merge?> Of(RepositorySet repos, IBoard board, Markers markers, int story, CancellationToken ct = default)
    {
        var merge = await repos.LandingOf(story, ct);
        var named = markers.FixOf(await board.Comments(story, ct), await repos.Default.Login(ct));
        if (named is null || (merge is not null && merge.At >= named.At)) return merge;
        // A merge of the story's own that waits for its pin is newer work than the fix it named.
        if (merge is null && await repos.AwaitsPin(story, ct)) return null;
        return new Merge(named.Shas[^1], named.At);
    }
}
