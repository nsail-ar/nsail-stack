// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;

namespace NSail.Harness.Planning;

public sealed record BoardSnapshot(
    IReadOnlyList<Issue> Issues,
    IReadOnlyList<StoryPr> Prs,
    Flight Flight,
    IReadOnlyList<ReleaseCandidate> Releases,
    DateTimeOffset? LastQaAt,
    DateTimeOffset? LastCutAt,
    IReadOnlyDictionary<(int Story, string Label), TimeSpan> LockAges,
    DateTimeOffset ReadAt,
    string Repo)
{
    // The holder of each story's lock: a harness's machine.
    public IReadOnlyDictionary<int, string> LockHolders { get; init; } = new Dictionary<int, string>();

    // The release buttons whose day's cut is due on this machine's clock (Steps.Release).
    public IReadOnlyList<string> DueReleases { get; init; } = [];

    // The Watch workers whose next pass is due and that run nowhere here.
    public IReadOnlyList<string> DueWatches { get; init; } = [];

    // main's head, and the last head of main a harness's gate judged or is judging (null: none yet).
    public string? MainSha { get; init; }
    public string? MainGated { get; init; }

    // The stories In-Progress with no open PR whose PR has merged (FilterOptions.PrMerged).
    public IReadOnlySet<int> MergedStories { get; init; } = new HashSet<int>();

    // The red-main stories in Ready whose newest merge no finished gate of main has judged yet: the
    // red that sent them back may be from a head older than the fix (SnapshotReader.ReadUnjudged).
    public IReadOnlySet<int> UnjudgedFixes { get; init; } = new HashSet<int>();

    // Per product (lowercase), its newest published release whose stories are not walked yet.
    public IReadOnlyDictionary<string, string> Unshipped { get; init; } = new Dictionary<string, string>();
}
