// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;

namespace NSail.Harness.Tests;

// A board written by hand: issues, PRs and what is in flight, nothing read from GitHub.
sealed class Fixture
{
    public const string Repo = "acme/app";
    public const string Stack = "acme/framework";

    readonly List<Issue> _issues = [];
    readonly List<StoryPr> _prs = [];
    readonly List<ReleaseCandidate> _releases = [];
    readonly Dictionary<(int, string), TimeSpan> _ages = [];
    readonly Dictionary<int, string> _holders = [];
    Flight _flight = Flight.Empty;
    readonly List<string> _due = [];
    DateTimeOffset? _lastQa;
    DateTimeOffset? _lastCut;
    readonly HashSet<int> _merged = [];
    readonly Dictionary<string, string> _unshipped = [];
    readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public Fixture Story(int n, string stage = "Ready", string labels = "", string? title = null,
        int subs = 0, int[]? blockers = null, double parentPercent = -1, int parent = 0, bool milestone = false, double ownPercent = 0, int? blockersTotal = null,
        TimeSpan? quiet = null)
    {
        _issues.Add(new Issue(n, title ?? $"story {n}",
            labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(),
            stage, subs, blockers ?? [], parentPercent, milestone, parent, ownPercent, blockersTotal ?? (blockers?.Length ?? 0))
        {
            TouchedAt = _now - quiet,
        });
        return this;
    }

    public Fixture Pr(int story, int number, CiState ci = CiState.Green, string? word = null, string repo = Repo, string sha = "abcdef1234567890", CiState? gate = null, TimeSpan? greenFor = null, bool shared = false)
    {
        _prs.Add(new StoryPr(story, number, sha, ci, repo, word, gate, _now - greenFor, shared));
        return this;
    }

    public Fixture Flying(int[]? dev = null, int[]? reviewPrs = null, int[]? analyze = null, bool qa = false)
    {
        _flight = new Flight((dev ?? []).ToHashSet(), (reviewPrs ?? []).ToHashSet(), (analyze ?? []).ToHashSet(), qa, new HashSet<string>());
        return this;
    }

    public Fixture Release(int story, string workflow = "release-shop.yml", string sha = "fixfixfixfix")
    {
        _releases.Add(new ReleaseCandidate(story, workflow, sha));
        return this;
    }

    public Fixture Due(string workflow)
    {
        _due.Add(workflow);
        return this;
    }

    public Fixture Published(string product, string tag)
    {
        _unshipped[product] = tag;
        return this;
    }

    public Fixture Shipping(string tag)
    {
        _flight = _flight with { ShipTags = _flight.ShipTags.Append(tag).ToHashSet() };
        return this;
    }

    public Fixture Cutting(string workflow)
    {
        _flight = _flight with { ReleaseWorkflows = _flight.ReleaseWorkflows.Append(workflow).ToHashSet() };
        return this;
    }

    public Fixture LockAge(int story, string label, TimeSpan age)
    {
        _ages[(story, label)] = age;
        return this;
    }

    public Fixture HeldBy(int story, string machine)
    {
        _holders[story] = machine;
        return this;
    }

    public Fixture MergedPr(int story)
    {
        _merged.Add(story);
        return this;
    }

    public Fixture Cuts(DateTimeOffset? lastCut, DateTimeOffset? lastQa)
    {
        _lastCut = lastCut;
        _lastQa = lastQa;
        return this;
    }

    public BoardSnapshot Build()
    {
        return new BoardSnapshot(_issues.OrderBy(i => i.Number).ToList(), _prs, _flight, _releases, _lastQa, _lastCut, _ages, _now, Repo)
        {
            LockHolders = _holders,
            DueReleases = _due,
            MergedStories = _merged,
            Unshipped = _unshipped,
        };
    }
}
