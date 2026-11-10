// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

public static class Capacity
{
    public static IReadOnlyList<Resource> Needs(WorkKind kind)
    {
        return kind switch
        {
            WorkKind.Dev or WorkKind.Review or WorkKind.Analyze => [Resource.Agent],
            WorkKind.Qa => [Resource.Agent, Resource.Browser],
            WorkKind.Release => [Resource.Build, Resource.Browser],
            WorkKind.Gate or WorkKind.Main => [Resource.Build],
            _ => [],
        };
    }

    // A story started from the queue holds its agent for hours: it never takes the last one, so a
    // review, a round or a QA drain that shows up meanwhile finds an agent free.
    public static readonly string[] Starting = ["p1", "epic", "rest"];

    // Walks the plan in order and takes every item whose resources are free right now:
    // with the builds full, the next agent-only item goes; with the agents full, whatever
    // needs none of them. Order is kept among what fits, never broken for what does not.
    // A worker's Max caps its own jobs on top: those running and those picked now.
    public static IReadOnlyList<WorkItem> Pick(IEnumerable<WorkItem> plan, IReadOnlyDictionary<Resource, int> inUse, Limits limits,
        IReadOnlyDictionary<string, int>? max = null, IReadOnlyDictionary<string, int>? running = null)
    {
        var used = Enum.GetValues<Resource>().ToDictionary(r => r, r => inUse.GetValueOrDefault(r));
        var byWorker = new Dictionary<string, int>(running ?? new Dictionary<string, int>());
        var picked = new List<WorkItem>();
        foreach (var item in plan)
        {
            var needs = Needs(item.Kind);
            if (needs.Any(r => used[r] >= limits.Of(r))) continue;
            if (Starting.Contains(item.Tier) && limits.Agents > 1 && used[Resource.Agent] >= limits.Agents - 1) continue;
            if (item.Worker is { } w && max?.GetValueOrDefault(w, int.MaxValue) is { } cap && byWorker.GetValueOrDefault(w) >= cap) continue;
            foreach (var r in needs) used[r]++;
            if (item.Worker is { } name) byWorker[name] = byWorker.GetValueOrDefault(name) + 1;
            picked.Add(item);
        }
        return picked;
    }
}
