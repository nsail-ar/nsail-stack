// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace NSail.Harness.Repositories;

// The repositories a story may be built in (HarnessOptions.Repositories), each with its options: the
// default one, and any an area label routes a story to.
public sealed class RepositorySet(IReadOnlyList<(IRepository Repository, RepositoryOptions Options)> repositories)
{
    public IRepository Default { get; } = repositories.Single(r => r.Options.Label is null).Repository;

    // Every repository, the default first.
    public IEnumerable<IRepository> All => repositories.OrderBy(r => r.Options.Label is null ? 0 : 1).Select(r => r.Repository);

    public IRepository Of(string name)
    {
        return repositories.FirstOrDefault(r => r.Repository.Name == name).Repository ?? Default;
    }

    public bool IsDefault(string name)
    {
        return Default.Name == name;
    }

    public RepositoryOptions OptionsOf(IRepository repository)
    {
        return repositories.Single(r => r.Repository == repository).Options;
    }

    // Where a new story is built. Its only area label routes it to that label's repository. A story
    // whose area labels name a routed repository beside other areas has two halves, the routed one
    // first: it builds there until a PR of it merged there (`landedIn`), then in the default repository.
    public IRepository RoutedBy(IEnumerable<string> labels, IReadOnlySet<string>? landedIn = null)
    {
        var areas = labels.Where(l => l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal)).ToList();
        var routed = repositories.Where(r => r.Options.Label is { } l && areas.Contains(l)).Select(r => r.Repository).ToList();
        if (routed.Count == 0) return Default;
        if (areas.Count == 1) return routed[0];
        return routed.FirstOrDefault(r => landedIn is null || !landedIn.Contains(r.Name)) ?? Default;
    }

    // RoutedBy, reading which routed repositories already hold a merge of the story.
    public async Task<IRepository> RoutedBy(int story, IEnumerable<string> labels, CancellationToken ct = default)
    {
        var areas = labels.ToList();
        var landed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (repository, options) in repositories)
        {
            if (options.Label is null || !areas.Contains(options.Label)) continue;
            if ((await repository.PrsOf(story, "merged", ct)).Count > 0) landed.Add(repository.Name);
        }
        return RoutedBy(areas, landed);
    }

    // The story's PRs in this state across every repository, the default first.
    public async Task<IReadOnlyList<PrRef>> PrsOf(int story, string state, CancellationToken ct = default)
    {
        var all = new List<PrRef>();
        foreach (var repo in All) all.AddRange(await repo.PrsOf(story, state, ct));
        return all;
    }

    // Where a pinned repository's merge reached the default branch never moves once found.
    readonly ConcurrentDictionary<(string Repo, string Sha), Merge> _landed = new();

    // The story's newest merge as the default branch holds it: its merge there, and for a merge in a
    // pinned repository the oldest commit of the default branch whose pin names a version that holds
    // it. Null with no merge, and while a pinned merge waits for its pin: the default branch does not
    // hold the story's work yet.
    public async Task<Merge?> LandingOf(int story, CancellationToken ct = default)
    {
        var landing = await Default.MergeOf(story, ct);
        foreach (var (repository, options) in repositories)
        {
            if (options.Label is null || options.Pin is not { } pin) continue;
            if (await repository.MergeOf(story, ct) is not { } merge) continue;
            if (await Pinned(repository, pin, merge, ct) is not { } bump) return null;
            if (landing is null || bump.At > landing.At) landing = bump;
        }
        return landing;
    }

    // A story routed to a pinned repository: its merge there reaches the default branch through the pin.
    public bool IsPinned(IEnumerable<string> labels)
    {
        return repositories.Any(r => r.Options is { Label: { } label, Pin: not null } && labels.Contains(label));
    }

    // A merge of the story in a pinned repository that the default branch's pin does not hold yet:
    // what depends on it cannot be built there. False with no merge in one — nothing waits.
    public async Task<bool> AwaitsPin(int story, CancellationToken ct = default)
    {
        foreach (var (repository, options) in repositories)
        {
            if (options.Label is null || options.Pin is not { } pin) continue;
            if (await repository.MergeOf(story, ct) is not { } merge) continue;
            if (await Pinned(repository, pin, merge, ct) is null) return true;
        }
        return false;
    }

    async Task<Merge?> Pinned(IRepository repository, PinOptions pin, Merge merge, CancellationToken ct)
    {
        if (_landed.TryGetValue((repository.Name, merge.Sha), out var found)) return found;
        foreach (var change in await Default.Changes(pin.File, merge.At, ct))
        {
            if (await Default.FileAt(pin.File, change.Sha, ct) is not { } text) continue;
            if (Regex.Match(text, pin.Pattern) is not { Success: true } version) continue;
            if (!await repository.Holds(pin.Ref.Replace("{version}", version.Groups[1].Value, StringComparison.Ordinal), merge.Sha, ct)) continue;
            _landed[(repository.Name, merge.Sha)] = change;
            return change;
        }
        return null;
    }
}
