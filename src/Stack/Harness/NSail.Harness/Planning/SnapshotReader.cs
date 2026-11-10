// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using NSail.Harness.Board;
using NSail.Harness.Locks;
using NSail.Harness.Repositories;

namespace NSail.Harness.Planning;

// Every read the planner needs, in one pass, from the board, the repositories and the locks. Each
// one answers whole or throws: a half read must never reach the planner as an empty queue.
public sealed class SnapshotReader(IBoard board, RepositorySet repos, IStoryLock locks, HarnessOptions options)
{
    readonly StoryNames _names = options.Names();
    readonly Markers _markers = new(options.ReviewMarker, options.MarkerAuthors);

    public async Task<BoardSnapshot> Read(Flight own, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var home = repos.Default;
        var running = await home.RunningWorkflows(ct);
        var flight = own with { ReleaseWorkflows = own.ReleaseWorkflows.Union(running.Where(Release)).ToHashSet() };
        var issues = (await board.Stories(ct)).ToList();
        var prs = new List<StoryPr>();
        var main = await home.Head(home.DefaultBranch, ct);
        foreach (var repo in repos.All) prs.AddRange(await ReadPrs(repo, main, ct));
        var releases = await home.Releases(ct);
        var (candidates, shipped) = await ReadRedFixes(issues, flight, releases, ct);
        return new BoardSnapshot(issues, prs, flight, candidates, null, releases.FirstOrDefault()?.PublishedAt,
            await ReadLockAges(issues, flight, now, ct), now, home.Name)
        {
            LockHolders = await locks.Holders(ct),
            MainSha = main,
            MergedStories = await ReadMerged(issues, prs, flight, ct),
            AwaitingPin = await ReadAwaitingPin(issues, ct),
            Unshipped = await ReadUnshipped(releases, ct),
            ShippedFixes = shipped,
        };
    }

    // A release is shipped once its version is closed on the board; the ones known shipped are
    // not asked again, so a pass costs a call only while a new release waits for its walk.
    static readonly ConcurrentDictionary<string, bool> Shipped = new();

    async Task<Dictionary<string, string>> ReadUnshipped(IReadOnlyList<Release> releases, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        foreach (var product in options.Releases.Select(r => r.Product?.ToLowerInvariant()).OfType<string>().Distinct())
        {
            if (releases.FirstOrDefault(r => r.Tag.StartsWith($"{product}/v", StringComparison.Ordinal)) is not { } newest) continue;
            if (Shipped.ContainsKey(newest.Tag)) continue;
            if (await board.VersionClosed(newest.Tag, ct)) Shipped[newest.Tag] = true;
            else result[product] = newest.Tag;
        }
        return result;
    }

    // A walk this harness finished: its version is closed, and the next pass need not ask.
    public static void MarkShipped(string tag)
    {
        Shipped[tag] = true;
    }

    // A story In-Progress with no open PR and no job on it was left there by one that died: whether
    // a PR of it merged says if it was built. Only those are asked, one call each — there are few.
    async Task<HashSet<int>> ReadMerged(List<Issue> issues, List<StoryPr> prs, Flight flight, CancellationToken ct)
    {
        var merged = new HashSet<int>();
        foreach (var i in issues.Where(i => i.Stage == Stages.InProgress && i.SubIssues == 0 && prs.All(p => p.Story != i.Number) && !flight.DevStories.Contains(i.Number)))
        {
            foreach (var repo in repos.All)
            {
                if (await repo.MergeOf(i.Number, ct) is null) continue;
                merged.Add(i.Number);
                break;
            }
        }
        return merged;
    }

    // A pin that took a merge keeps it: a blocker found held is not asked again while it stays Merged.
    readonly HashSet<int> _pinHeld = [];

    // A blocker merged in a pinned repository lands for what waits on it once the default branch's pin
    // holds the merge; a release that carries it means the pin did. Only the Merged blockers some
    // story still names, routed to a pinned repository, are asked — a call or two each, and there are few.
    internal async Task<HashSet<int>> ReadAwaitingPin(IReadOnlyList<Issue> issues, CancellationToken ct = default)
    {
        var merged = issues.Where(i => i.Stage == Stages.Merged && repos.IsPinned(i.Labels)).Select(i => i.Number).ToHashSet();
        _pinHeld.IntersectWith(merged);
        var awaiting = new HashSet<int>();
        foreach (var blocker in issues.SelectMany(i => i.OpenBlockers).Distinct())
        {
            if (!merged.Contains(blocker) || _pinHeld.Contains(blocker)) continue;
            if (await repos.AwaitsPin(blocker, ct)) awaiting.Add(blocker);
            else _pinHeld.Add(blocker);
        }
        return awaiting;
    }

    // The story's open PR, read now in every repository from the whole list (the search index lags
    // the minute a PR opens), as `owner/name PR #n`; null when it has none.
    public async Task<string?> OpenPrOf(int story, CancellationToken ct = default)
    {
        foreach (var repo in repos.All)
        {
            foreach (var pr in await repo.OpenPrs(ct))
            {
                if (IsCrews(pr, repo) && _names.StoryOf(pr.Title, pr.Head) == story) return $"{repo.Name} PR #{pr.Number}";
            }
        }
        return null;
    }

    bool Release(string workflow)
    {
        return options.Releases.Any(r => r.Workflow == workflow);
    }

    internal async Task<List<StoryPr>> ReadPrs(IRepository repo, string main, CancellationToken ct)
    {
        var home = repo == repos.Default;
        var result = new List<StoryPr>();
        CiFailure? mains = null;
        var mainRead = false;
        var open = await repo.OpenPrs(ct);
        foreach (var gone in FailureByPr.Keys.Where(k => k.Repo == repo.Name && open.All(p => p.Number != k.Pr)))
        {
            FailureByPr.TryRemove(gone, out _);
        }
        foreach (var pr in open)
        {
            if (!IsCrews(pr, repo)) continue;
            if (_names.StoryOf(pr.Title, pr.Head) is not { } story) continue;
            // A red the CI decides (no gate judges this repository) that main's own newest CI shares is no round.
            var shared = false;
            if (pr.Ci == CiState.Red && !(home && options.Gate))
            {
                if (!mainRead)
                {
                    mains = await repo.MainFailure(ct);
                    mainRead = true;
                }
                shared = mains is not null && (await FailureOf(repo, pr, ct))?.SharedWith(mains) == true;
            }
            else
            {
                FailureByPr.TryRemove((pr.Repo, pr.Number), out _);
            }
            // The default repository's heads always need their threads (the gate's verdict lives there);
            // another's head only once its own CI is green.
            var bodies = home || pr.Ci == CiState.Green ? await Bodies(repo, pr, ct) : [];
            var gate = home && options.Gate ? Markers.GateAt(bodies, pr.Sha, main) : null;
            var green = gate == CiState.Green || (gate is null && pr.Ci == CiState.Green);
            var word = green ? _markers.ReviewAt(bodies, pr.Sha) : null;
            result.Add(new StoryPr(story, pr.Number, pr.Sha, pr.Ci, pr.Repo, word, gate, pr.GreenAt, shared));
        }
        return result;
    }

    // What a red head's CI failed is read once while it stays red (a run again turns it pending
    // first), one entry per open PR.
    static readonly ConcurrentDictionary<(string Repo, int Pr), (string Sha, CiFailure? Failure)> FailureByPr = new();

    static async Task<CiFailure?> FailureOf(IRepository repo, OpenPr pr, CancellationToken ct)
    {
        if (FailureByPr.TryGetValue((pr.Repo, pr.Number), out var hit) && hit.Sha == pr.Sha) return hit.Failure;
        var failure = await repo.Failure(pr.Sha, ct);
        FailureByPr[(pr.Repo, pr.Number)] = (pr.Sha, failure);
        return failure;
    }

    internal static bool OwnsHead(string headRepo, string repoName)
    {
        return string.Equals(headRepo, repoName, StringComparison.OrdinalIgnoreCase);
    }

    // A PR is the crew's only when its head lives in this repository (a fork's branch name is the
    // fork's to choose) and, where the repository lists its authors, one of them opened it.
    internal static bool IsCrews(OpenPr pr, string repoName, IReadOnlyCollection<string> authors)
    {
        if (!OwnsHead(pr.HeadRepo, repoName)) return false;
        return authors.Count == 0 || authors.Contains(pr.Author, StringComparer.OrdinalIgnoreCase);
    }

    bool IsCrews(OpenPr pr, IRepository repo)
    {
        return IsCrews(pr, repo.Name, repos.OptionsOf(repo).Authors);
    }

    // A PR's thread only changes when the PR does: it is read again only when its updatedAt moves,
    // or every pass would cost two calls per open PR.
    static readonly ConcurrentDictionary<(string Repo, int Pr), (string UpdatedAt, List<string> Bodies)> BodiesByPr = new();

    async Task<List<string>> Bodies(IRepository repo, OpenPr pr, CancellationToken ct)
    {
        if (BodiesByPr.TryGetValue((pr.Repo, pr.Number), out var hit) && hit.UpdatedAt == pr.UpdatedAt) return hit.Bodies;
        var bodies = _markers.Bodies(await repo.Thread(pr.Number, ct), await repo.Login(ct)).ToList();
        BodiesByPr[(pr.Repo, pr.Number)] = (pr.UpdatedAt, bodies);
        return bodies;
    }

    // How long each lock label has been on its story, for the ones no job of this harness holds.
    async Task<Dictionary<(int, string), TimeSpan>> ReadLockAges(List<Issue> issues, Flight flight, DateTimeOffset now, CancellationToken ct)
    {
        var ages = new Dictionary<(int, string), TimeSpan>();
        foreach (var i in issues)
        {
            var labels = new List<string>();
            if (i.Has(Labels.Claimed) && !flight.DevStories.Contains(i.Number)) labels.Add(Labels.Claimed);
            if (i.Has(Labels.Review)) labels.Add(Labels.Review);
            if (i.Has(Labels.Analyzing) && !flight.AnalyzeStories.Contains(i.Number)) labels.Add(Labels.Analyzing);
            if (labels.Count == 0) continue;
            foreach (var (label, at) in await board.LabeledAt(i.Number, labels, ct)) ages[(i.Number, label)] = now - at;
        }
        return ages;
    }

    // A red-main story in Ready with a merge waits until a gate of main judges a head that holds it:
    // the newest gate verdict along main is on a head that holds the merge, or none does yet (the
    // running gate's pending mark is no verdict). Only the stories with a merge cost a call or two.
    internal async Task<IReadOnlySet<int>> ReadUnjudged(IReadOnlyList<Issue> issues, IReadOnlyList<StatusMark> marks, CancellationToken ct = default)
    {
        var unjudged = new HashSet<int>();
        var judged = StatusMark.Newest(marks, Markers.GateContext)?.Sha;
        foreach (var i in issues.Where(i => i.Stage == Stages.Ready && i.Title.StartsWith(options.Texts.MainRedTitle, StringComparison.Ordinal)))
        {
            if (await repos.LandingOf(i.Number, ct) is not { } merge) continue;
            if (judged is null || !await repos.Default.Holds(judged, merge.Sha, ct)) unjudged.Add(i.Number);
        }
        return unjudged;
    }

    // The merged red-suite stories, as two answers. Candidates: a fix merged, nobody cutting its
    // product, and fewer than two cuts since the merge; the release job runs the suite on main's
    // head, which holds the fix. Shipped: the product published a release after the merge, so the
    // fix is out whatever the walk of that release has or has not moved yet (a cut that red-failed
    // after the merge sends the story back to Ready, so a Merged story has no such cut).
    internal async Task<(List<ReleaseCandidate> Candidates, HashSet<int> Shipped)> ReadRedFixes(List<Issue> issues, Flight flight, IReadOnlyList<Release> releases, CancellationToken ct)
    {
        var result = new List<ReleaseCandidate>();
        var shipped = new HashSet<int>();
        foreach (var i in issues.Where(i => i.Stage == Stages.Merged))
        {
            // Only a red suite names its product; a red main's fix is cut with the next day's cut.
            var release = options.Releases.FirstOrDefault(r => i.Title == HarnessTexts.Fill(options.Texts.SuiteRedTitle, ("product", r.Product!)));
            if (release?.Workflow is not { } wf) continue;
            if (await repos.LandingOf(i.Number, ct) is not { } merge) continue;
            var product = release.Product!;
            if (releases.Any(r => r.Tag.StartsWith($"{product.ToLowerInvariant()}/v", StringComparison.Ordinal) && r.PublishedAt > merge.At))
            {
                shipped.Add(i.Number);
                continue;
            }
            if (flight.ReleaseWorkflows.Contains(wf)) continue;
            if (await repos.Default.RunsSince(wf, merge.At, ct) >= 2) continue;
            result.Add(new ReleaseCandidate(i.Number, wf, merge.Sha));
        }
        return (result, shipped);
    }
}
