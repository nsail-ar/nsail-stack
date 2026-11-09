// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Workers;

namespace NSail.Harness.Planning;

// The release andon. A product whose cut failed has an open red story (the one the Release worker
// files when its suite is red); until that story ships, the release takes the whole harness: the
// planner starts only the red story's analysis, build and PR work, a red main's, main's gate and the
// product's own cut. Jobs already running finish. A hand lifts it by closing the story or moving it
// to Released, or its product published a release after the fix merged (ShippedFixes).
public static class ReleaseAndon
{
    // The tier above `bug`: the most urgent turn there is.
    public const string Tier = "andon";

    // A story out of these has shipped (or is past it): its red no longer holds the line.
    static readonly string[] Shipped = [Stages.Released, Stages.Qa, Stages.Closed];

    // The products lit now, one per Release worker whose product has an open red story that has not shipped.
    public static IReadOnlyList<Andon> Read(IReadOnlyList<Issue> issues, IReadOnlySet<int> shipped, IReadOnlyList<Worker> workers, PlannerOptions options)
    {
        var lit = new List<Andon>();
        foreach (var product in workers.Where(w => w.Kind == WorkKind.Release && w.Options.Product is not null).Select(w => w.Options.Product!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var title = options.SuiteRedTitleOf(product);
            if (issues.Where(i => i.Title == title && !Shipped.Contains(i.Stage) && !shipped.Contains(i.Number)).OrderBy(i => i.Number).FirstOrDefault() is { } story)
            {
                lit.Add(new Andon(product, story.Number, story.CreatedAt, WaitsFor(story)));
            }
        }
        return lit;
    }

    // The stories whose work is the release's: each lit product's red story and a red main's, which
    // may be what keeps the product's suite red.
    public static IReadOnlySet<int> Stories(IReadOnlyList<Issue> issues, IReadOnlyList<Andon> lit, PlannerOptions options)
    {
        var stories = lit.Select(a => a.Story).ToHashSet();
        foreach (var i in issues.Where(i => i.Title.StartsWith(options.MainRedPrefix, StringComparison.Ordinal))) stories.Add(i.Number);
        return stories;
    }

    // Whether work on its own (a cut, a walk, main's gate) belongs to the release: main's gate always
    // (it judges the fix's merge); a cut or a walk only of a lit product; anything else never.
    public static bool Owns(WorkItem item, IReadOnlyList<Andon> lit)
    {
        return item.Kind switch
        {
            WorkKind.Main => true,
            WorkKind.Release or WorkKind.Ship => lit.Any(a => string.Equals(a.Product, item.Product, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    // What the release waits for, read off where its red story stands.
    internal static string WaitsFor(Issue story)
    {
        foreach (var hold in new[] { Labels.Ruling, Labels.Hand, Labels.Blocked, Labels.Stalled })
        {
            if (story.Has(hold)) return $"a hand: #{story.Number} wears `{hold}`";
        }
        return story.Stage switch
        {
            Stages.Merged => $"the cut: the fix of #{story.Number} merged",
            Stages.InProgress => $"the fix of #{story.Number}: its PR, then its merge",
            Stages.Ready => $"a builder for the fix of #{story.Number}",
            _ => $"the analysis of #{story.Number} ({story.Stage})",
        };
    }
}
