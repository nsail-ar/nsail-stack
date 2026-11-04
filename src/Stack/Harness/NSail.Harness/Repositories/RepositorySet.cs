// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    // Where a new story is built: the repository its only area label routes it to, else the default.
    public IRepository RoutedBy(IEnumerable<string> labels)
    {
        var areas = labels.Where(l => l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal)).ToList();
        if (areas.Count != 1) return Default;
        return repositories.FirstOrDefault(r => r.Options.Label == areas[0]).Repository ?? Default;
    }

    // The story's PRs in this state across every repository, the default first.
    public async Task<IReadOnlyList<PrRef>> PrsOf(int story, string state, CancellationToken ct = default)
    {
        var all = new List<PrRef>();
        foreach (var repo in All) all.AddRange(await repo.PrsOf(story, state, ct));
        return all;
    }
}
