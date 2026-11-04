// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Jobs;

// A product's published release, walked on the board: every story its commits name ships in its
// version, and every Merged story whose work the release holds moves to Released. A story's work is
// held when each of its merged PRs is: one in the default repository is in the tag's history (or
// carried onto the release branch by a cherry-pick naming it), one in a pinned repository is in the
// version the tag pins (RepositoryOptions.Pin), one in a repository with no pin rides any release.
// A story wearing only another product's area waits for that product's release. The version is
// closed last, and closed is what says the release was walked (SnapshotReader.ReadUnshipped).
public sealed class ShipJob(WorkItem item, JobContext ctx) : IJob
{
    public WorkItem Item { get; } = item;

    public async Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
    {
        var tag = Item.Tag!;
        var p = Item.Product!;
        var dir = await ctx.Workspace.Prepare(slot);
        await using var w = new StreamWriter(logPath, append: true) { AutoFlush = true };
        await Shell.Git(dir, "fetch", "-q", "--force", "origin", $"+refs/tags/{p}/*:refs/tags/{p}/*");
        var tags = (await Shell.Git(dir, "tag", "-l", $"{p}/v*", "--sort=-v:refname")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var last = tags.SkipWhile(t => t != tag).Skip(1).FirstOrDefault();
        var range = last is null ? tag : $"{last}..{tag}";
        var log = await Shell.Git(dir, "log", "--format=%B", range);
        await w.WriteLineAsync($"== ship {tag} (since {last ?? "the first commit"}) on {ctx.Options.Machine}");

        foreach (var n in Named(log, ctx.Names.Key))
        {
            try
            {
                await ctx.Board.SetVersion(n, tag, ct);
            }
            // A name in a message that is no story (a PR's number, a typo) ships nothing.
            catch (ProviderException e)
            {
                await w.WriteLineAsync($"{ctx.Names.Ref(n)} named, not set: {e.Message}");
            }
        }

        var products = ctx.Options.Releases.Select(r => r.Product?.ToLowerInvariant()).OfType<string>().ToHashSet();
        var moved = new List<int>();
        foreach (var story in (await ctx.Board.Stories(ct)).Where(s => s.Stage == Stages.Merged && !Waits(s.Labels, p, products)))
        {
            var prs = await ctx.Repos.PrsOf(story.Number, "merged", ct);
            if (prs.Count == 0) continue;
            var held = true;
            foreach (var pr in prs) held &= await Holds(pr, tag, log, dir, ct);
            await w.WriteLineAsync($"{ctx.Names.Ref(story.Number)}: {(held ? "held" : "not held")} by {tag} ({string.Join(", ", prs.Select(pr => $"{pr.Repo}#{pr.Number}"))})");
            if (!held) continue;
            await ctx.Board.SetVersion(story.Number, tag, ct);
            await ctx.Board.SetStage(story.Number, Stages.Released, ct);
            moved.Add(story.Number);
        }

        await ctx.Board.CloseVersion(tag, ct);
        SnapshotReader.MarkShipped(tag);
        return new JobOutcome(moved.Count == 0 ? $"{tag} walked: no Merged story it holds" : $"{tag} walked: {string.Join(", ", moved.Select(n => $"#{n}"))} Released");
    }

    async Task<bool> Holds(PrRef pr, string tag, string log, string dir, CancellationToken ct)
    {
        if (pr.MergeSha is not { } sha) return false;
        if (ctx.Repos.IsDefault(pr.Repo))
        {
            var ancestor = await Shell.Run("git", ["merge-base", "--is-ancestor", sha, tag], dir);
            return ancestor.ExitCode == 0 || log.Contains($"cherry picked from commit {sha}", StringComparison.Ordinal);
        }
        var repo = ctx.Repos.Of(pr.Repo);
        if (ctx.Repos.OptionsOf(repo).Pin is not { } pin) return true;
        var file = await Shell.Run("git", ["show", $"{tag}:{pin.File}"], dir);
        if (file.ExitCode != 0 || Regex.Match(file.Output, pin.Pattern) is not { Success: true } m) return false;
        return await repo.Holds(pin.Ref.Replace("{version}", m.Groups[1].Value, StringComparison.Ordinal), sha, ct);
    }

    // Whether a story waits for another product's release: it wears an area of a product with a
    // release, and not this one's. A story wearing none of them ships with whichever comes first.
    internal static bool Waits(IReadOnlySet<string> labels, string product, IReadOnlySet<string> products)
    {
        var worn = products.Where(q => labels.Contains($"{Labels.AreaPrefix}{q}")).ToList();
        return worn.Count > 0 && !worn.Contains(product);
    }

    // The stories a release's commit messages name (`{key}#{n}`), once each.
    internal static IReadOnlyList<int> Named(string log, string key)
    {
        return Regex.Matches(log, $@"\b{Regex.Escape(key)}#(\d+)").Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Distinct().Order().ToList();
    }
}
