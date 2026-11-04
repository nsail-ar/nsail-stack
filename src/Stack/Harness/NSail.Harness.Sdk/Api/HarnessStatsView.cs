// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// What this machine's crew did over a range: its jobs by role, bucket by bucket (hours for 24h,
// days otherwise), each role's outcomes and times, and the stories it carried to a merge. A job
// that found nothing to do, or ended on the agent's quota wall, is not counted. Durations and waits
// are in seconds, read off a histogram, so they are the upper edge of their bucket.
public sealed class HarnessStatsView
{
    public required string Range { get; init; }
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public required IReadOnlyList<StatsBucket> Buckets { get; init; }
    public required IReadOnlyList<RoleStats> Roles { get; init; }
    public required StatsTotals Totals { get; init; }

    // When the harness started in the range, and on which commit: a change of version or settings.
    public required IReadOnlyList<HarnessStart> Starts { get; init; }
}

public sealed class StatsBucket
{
    public DateTimeOffset At { get; init; }
    public required IReadOnlyList<RoleCount> Jobs { get; init; }
}

public sealed class RoleCount
{
    public required string Role { get; init; }
    public int Count { get; init; }
}

// Ok: its work went as it should; Failed: its work went wrong (a red build, a dead voyage);
// Broke: the harness's own break. Wait: from first planned to started.
public sealed class RoleStats
{
    public required string Role { get; init; }
    public int Jobs { get; init; }
    public int Ok { get; init; }
    public int Failed { get; init; }
    public int Broke { get; init; }
    public double? P50 { get; init; }
    public double? P95 { get; init; }
    public double? Wait { get; init; }
}

// Previous: the jobs of the range just before, for the trend. Merged: the PRs its reviewer landed.
// LeadTime: from the first job the harness ran on a story to its PR landed, the median.
public sealed class StatsTotals
{
    public int Jobs { get; init; }
    public int Ok { get; init; }
    public int Previous { get; init; }
    public int Merged { get; init; }
    public double? LeadTime { get; init; }
    public double? DevRoundsPerStory { get; init; }
    public int Reviews { get; init; }
    public int ReviewsWithFindings { get; init; }
}

public sealed class HarnessStart
{
    public DateTimeOffset At { get; init; }
    public string? Version { get; init; }
}
