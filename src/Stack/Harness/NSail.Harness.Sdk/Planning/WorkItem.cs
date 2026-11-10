// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// Story is the story the item moves (for Review, the story of the PR); Pr is set for Review.
// Tier says which rung of the order planned it: andon, bug, release, red, round, review, qa, stalled, analyze, p1, epic, rest.
// PrRepo is the repository the PR lives in (the default one, or another a story was built in), when the item rounds a PR.
// A Release names its product's button (Workflow, `release-web.yml`) and, when a red fix sent it, the fix's story.
// Worker names the worker (Harness:Workers) that planned it, when workers plan.
// A Ship names the release (Tag, `web/v1.2.0`) whose stories it moves to Released. A Watch is its worker.
public sealed record WorkItem(WorkKind Kind, int? Story, int? Pr, string Tier, string Why, string? Workflow = null, string? Sha = null, string? PrRepo = null, string? Worker = null, string? ProductName = null, string? Tag = null)
{
    // The product a release cuts, lowercase: its worker's, else read off its button (`release-web.yml`).
    public string? Product => ProductName?.ToLowerInvariant() ?? (Workflow is null ? null : Path.GetFileNameWithoutExtension(Workflow)["release-".Length..]);

    public override string ToString()
    {
        return Kind switch
        {
            WorkKind.Release => $"release {Product} ({Why})",
            WorkKind.Ship => $"ship {Tag}",
            WorkKind.Watch => $"watch {Worker}",
            WorkKind.Review => $"review PR #{Pr} (story #{Story})",
            WorkKind.Gate => $"gate PR #{Pr} (story #{Story})",
            WorkKind.Main => $"gate main at {Sha?[..Math.Min(8, Sha.Length)]}",
            WorkKind.Qa => Story is { } s ? $"qa #{s}" : "qa drain",
            _ => $"{Kind.ToString().ToLowerInvariant()} #{Story} ({Why})",
        };
    }
}
