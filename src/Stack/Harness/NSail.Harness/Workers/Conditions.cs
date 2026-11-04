// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Cronos;
using NSail.Harness.Agents;

namespace NSail.Harness.Workers;

// What a worker's When reads: the time, the machine's load (averaged over each condition's own
// window) and the agent's quota windows as its last run reported them.
public sealed record Readings(
    DateTimeOffset Now,
    Func<TimeSpan, double?> Cpu,
    Func<TimeSpan, double?> Memory,
    IReadOnlyList<UsageWindow> Usage);

// A worker's When against this pass's readings: every condition set must hold. A reading the harness
// does not have yet (no CPU samples since the start, no agent run since its quota reset) does not stop
// a worker: it holds until a reading says otherwise, or a fresh harness would never start an agent.
public static class Conditions
{
    public static bool Hold(WhenOptions when, Readings r, IReadOnlyCollection<string> down)
    {
        if (when.Reachable.Any(down.Contains)) return false;
        // A schedule with no End is a firing (a day's cut): its type reads it, it bounds nothing here.
        if (when.Schedule is { End.Length: > 0 } schedule && !IsOpen(schedule, r.Now)) return false;
        if (when.Cpu is { } cpu && r.Cpu(TimeSpan.FromMinutes(cpu.Minutes)) is { } c && !cpu.Holds(c)) return false;
        if (when.Memory is { } memory && r.Memory(TimeSpan.FromMinutes(memory.Minutes)) is { } m && !memory.Holds(m)) return false;
        if (when.Usage is { } usage && !Holds(usage, r)) return false;
        return true;
    }

    static bool Holds(UsageCondition usage, Readings r)
    {
        // A window whose reset has passed since the reading is a fresh one: nothing known of it yet.
        if (r.Usage.FirstOrDefault(w => w.Name == usage.Window) is not { } window || window.ResetsAt <= r.Now) return true;
        var used = window.Utilization * 100;
        if (usage.Above is { } above && used <= above) return false;
        if (usage.Below is { } below && used >= below) return false;
        var pace = window.PaceAt(r.Now);
        if (usage.PaceAbove is { } paceAbove && pace <= paceAbove) return false;
        if (usage.PaceBelow is { } paceBelow && pace >= paceBelow) return false;
        return true;
    }

    // Open between a Start and the End after it: the next End coming before the next Start.
    public static bool IsOpen(ScheduleCondition schedule, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone);
        var start = CronExpression.Parse(schedule.Start).GetNextOccurrence(now, zone);
        var end = CronExpression.Parse(schedule.End).GetNextOccurrence(now, zone);
        if (end is null) return false;
        return start is null || end < start;
    }

    // The schedule's last Start at or before now, within the last week; null if none.
    public static DateTimeOffset? LastFiring(ScheduleCondition schedule, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone);
        return CronExpression.Parse(schedule.Start).GetOccurrences(now.AddDays(-7), now, zone, fromInclusive: true, toInclusive: true)
            .Cast<DateTimeOffset?>().LastOrDefault();
    }

    // What is wrong with a When, if anything, so a bad one stops the harness at start.
    public static IEnumerable<string> Problems(string where, WhenOptions when)
    {
        if (when.Schedule is { } s)
        {
            foreach (var (field, cron) in new[] { ("Start", s.Start), ("End", s.End) }.Where(p => p.Item1 == "Start" || p.Item2.Length > 0))
            {
                string? problem = null;
                try
                {
                    CronExpression.Parse(cron);
                }
                catch (CronFormatException e)
                {
                    problem = $"{where}: Schedule.{field} \"{cron}\" is not a cron expression ({e.Message})";
                }
                if (problem is not null) yield return problem;
            }
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(s.TimeZone, out _)) yield return $"{where}: Schedule.TimeZone \"{s.TimeZone}\" is not a time zone";
        }
        foreach (var (field, load) in new[] { ("Cpu", when.Cpu), ("Memory", when.Memory) })
        {
            if (load is null) continue;
            if (load.Above is null && load.Below is null) yield return $"{where}: {field} names neither Above nor Below";
            if (load.Minutes < 1) yield return $"{where}: {field}.Minutes must be at least 1";
        }
        if (when.Usage is { } u && u.Above is null && u.Below is null && u.PaceAbove is null && u.PaceBelow is null)
        {
            yield return $"{where}: Usage names no bound (Above, Below, PaceAbove, PaceBelow)";
        }
    }
}
