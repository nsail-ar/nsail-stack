// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Harness.Api;
using NSail.Harness.Jobs;
using NSail.Harness.Planning;

namespace NSail.Harness.Lanes;

// What this machine's crew did, counted as it happens, in one small file beside the job history
// (stats.json): each job that ends adds to its role's tally in its hour (kept two days) and its
// day (kept ninety, on the machine's clock), its time and wait into a histogram rather than as a
// sample, so a read never walks the jobs and the file never grows past its windows. Like the job
// history it is a log, never state: a file lost or unreadable starts the count again.
public sealed class JobStats
{
    public static readonly string[] Roles = ["analyst", "dev", "gate", "reviewer", "qa", "release", "watch"];

    // A histogram's upper edges in seconds: 15 s, each half again the one before, the last past
    // two days; one more bucket holds what passes it.
    internal static readonly double[] Edges = [.. Enumerable.Range(0, 24).Select(i => 15 * Math.Pow(1.5, i))];

    static readonly TimeSpan HoursKept = TimeSpan.FromHours(48);
    static readonly TimeSpan DaysKept = TimeSpan.FromDays(90);
    static readonly JsonSerializerOptions Json = new();

    readonly string? _path;
    readonly TimeZoneInfo _zone;
    readonly Lock _gate = new();
    readonly StatsData _data;

    // No root keeps the count in memory only.
    public JobStats(string? root, TimeZoneInfo? zone = null, ILogger? log = null)
    {
        _zone = zone ?? TimeZoneInfo.Local;
        if (root is null)
        {
            _data = new StatsData();
            return;
        }
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "stats.json");
        _data = StateFile.Read(_path, () => new StatsData(), Json, log);
    }

    public static string RoleOf(WorkKind kind)
    {
        return kind switch
        {
            WorkKind.Analyze => "analyst",
            WorkKind.Dev => "dev",
            WorkKind.Review => "reviewer",
            WorkKind.Qa => "qa",
            WorkKind.Release or WorkKind.Ship => "release",
            WorkKind.Gate or WorkKind.Main => "gate",
            WorkKind.Watch => "watch",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // The harness came up, on this commit.
    public void Begun(string? version, DateTimeOffset at)
    {
        lock (_gate)
        {
            _data.Starts.Add(new HarnessStart { At = at, Version = version });
            Prune(at);
            Save();
        }
    }

    // A job started on a story: the first one starts the story's lead time.
    public void Started(int? story, DateTimeOffset at)
    {
        if (story is not { } n) return;
        lock (_gate)
        {
            _data.FirstSeen.TryAdd(n, at);
        }
    }

    // A job ended: with no outcome it broke. One that found nothing to do, or met the quota wall, is
    // not the crew's work and is not counted; nor is one that ended on the provider, which the caller
    // never hands here.
    public void Ended(WorkKind kind, int? story, DateTimeOffset started, DateTimeOffset ended, TimeSpan? waited, JobOutcome? outcome)
    {
        if (outcome is { Quiet: true }) return;
        var role = RoleOf(kind);
        lock (_gate)
        {
            foreach (var slot in new[] { SlotOf(_data.Hours, HourOf(ended)), SlotOf(_data.Days, DayOf(ended)) })
            {
                if (!slot.Roles.TryGetValue(role, out var tally)) slot.Roles[role] = tally = new Tally();
                if (outcome is null) tally.Broke++;
                else if (outcome.Failed) tally.Failed++;
                else tally.Ok++;
                if (outcome?.Event == "Findings") tally.Findings++;
                tally.Took[Bucket(ended - started)]++;
                if (waited is { } w) tally.Waited[Bucket(w)]++;
                if (story is { } n && !tally.Stories.Contains(n)) tally.Stories.Add(n);
            }
            Prune(ended);
            Save();
        }
    }

    // A PR the reviewer landed: its story's lead time ends here.
    public void Merged(int pr, int? story, DateTimeOffset at)
    {
        lock (_gate)
        {
            _data.Merges.Add(new MergeMark(at, pr));
            if (story is { } n && _data.FirstSeen.Remove(n, out var first)) _data.Leads.Add(new LeadMark(at, n, (at - first).TotalSeconds));
            Prune(at);
            Save();
        }
    }

    public HarnessStatsView View(string range, DateTimeOffset now)
    {
        var days = range switch { "7d" => 7, "30d" => 30, _ => 0 };
        var hourly = days == 0;
        var step = hourly ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var count = hourly ? 24 : days;
        var to = hourly ? HourOf(now) + step : Days(DayOf(now), 1);
        var from = hourly ? to - TimeSpan.FromHours(24) : Days(DayOf(now), 1 - days);
        var previous = hourly ? from - TimeSpan.FromHours(24) : Days(from, -days);
        lock (_gate)
        {
            var slots = hourly ? _data.Hours : _data.Days;
            var inRange = slots.Where(s => s.At >= from && s.At < to).ToList();
            var tallies = Roles.ToDictionary(r => r, r => Sum(inRange.Select(s => s.Roles.GetValueOrDefault(r)).OfType<Tally>()));
            var buckets = Enumerable.Range(0, count).Select(i =>
            {
                var at = hourly ? from + step * i : Days(from, i);
                var slot = inRange.FirstOrDefault(s => s.At == at);
                return new StatsBucket
                {
                    At = at,
                    Jobs = [.. Roles.Select(r => new RoleCount { Role = r, Count = slot?.Roles.GetValueOrDefault(r)?.Jobs ?? 0 })],
                };
            }).ToList();
            var merged = _data.Merges.Count(m => m.At >= from && m.At < to);
            var leads = _data.Leads.Where(l => l.At >= from && l.At < to).Select(l => l.Seconds).Order().ToList();
            var dev = tallies["dev"];
            var reviewer = tallies["reviewer"];
            return new HarnessStatsView
            {
                Range = hourly ? "24h" : $"{days}d",
                From = from,
                To = to,
                Buckets = buckets,
                Roles = [.. Roles.Select(r => tallies[r]).Select((t, i) => new RoleStats
                {
                    Role = Roles[i],
                    Jobs = t.Jobs,
                    Ok = t.Ok,
                    Failed = t.Failed,
                    Broke = t.Broke,
                    P50 = Percentile(t.Took, 0.5),
                    P95 = Percentile(t.Took, 0.95),
                    Wait = Percentile(t.Waited, 0.5),
                })],
                Totals = new StatsTotals
                {
                    Jobs = tallies.Values.Sum(t => t.Jobs),
                    Ok = tallies.Values.Sum(t => t.Ok),
                    Previous = slots.Where(s => s.At >= previous && s.At < from).Sum(s => s.Roles.Values.Sum(t => t.Jobs)),
                    Merged = merged,
                    LeadTime = leads.Count == 0 ? null : leads[(leads.Count - 1) / 2],
                    DevRoundsPerStory = dev.Stories.Count == 0 ? null : Math.Round((double)dev.Jobs / dev.Stories.Count, 1),
                    Reviews = reviewer.Jobs,
                    ReviewsWithFindings = reviewer.Findings,
                },
                Starts = [.. _data.Starts.Where(s => s.At >= from && s.At < to)],
            };
        }
    }

    // The upper edge of the bucket the share p of the counts falls in; null with no counts.
    internal static double? Percentile(int[] histogram, double p)
    {
        var total = histogram.Sum();
        if (total == 0) return null;
        var rank = Math.Max(1, (int)Math.Ceiling(p * total));
        var seen = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            seen += histogram[i];
            if (seen >= rank) return i < Edges.Length ? Edges[i] : Edges[^1] * 1.5;
        }
        return Edges[^1] * 1.5;
    }

    internal static int Bucket(TimeSpan span)
    {
        var at = Array.FindIndex(Edges, e => span.TotalSeconds <= e);
        return at < 0 ? Edges.Length : at;
    }

    static Tally Sum(IEnumerable<Tally> tallies)
    {
        var sum = new Tally();
        foreach (var t in tallies)
        {
            sum.Ok += t.Ok;
            sum.Failed += t.Failed;
            sum.Broke += t.Broke;
            sum.Findings += t.Findings;
            for (var i = 0; i < sum.Took.Length; i++)
            {
                sum.Took[i] += t.Took.ElementAtOrDefault(i);
                sum.Waited[i] += t.Waited.ElementAtOrDefault(i);
            }
            foreach (var n in t.Stories.Where(n => !sum.Stories.Contains(n))) sum.Stories.Add(n);
        }
        return sum;
    }

    static StatsSlot SlotOf(List<StatsSlot> slots, DateTimeOffset at)
    {
        if (slots.FirstOrDefault(s => s.At == at) is { } found) return found;
        var slot = new StatsSlot { At = at };
        slots.Add(slot);
        return slot;
    }

    static DateTimeOffset HourOf(DateTimeOffset at)
    {
        var utc = at.UtcDateTime;
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    // Midnight of the machine's day, so "today" is the person's and not UTC's.
    DateTimeOffset DayOf(DateTimeOffset at)
    {
        var local = TimeZoneInfo.ConvertTime(at, _zone).Date;
        return new DateTimeOffset(local, _zone.GetUtcOffset(local));
    }

    // A day's midnight moved by whole days, at the offset that day has in the machine's zone.
    DateTimeOffset Days(DateTimeOffset midnight, int days)
    {
        return DayOf(midnight.AddDays(days).AddHours(12));
    }

    void Prune(DateTimeOffset now)
    {
        _data.Hours.RemoveAll(s => s.At < now - HoursKept);
        _data.Days.RemoveAll(s => s.At < now - DaysKept);
        _data.Merges.RemoveAll(m => m.At < now - DaysKept);
        _data.Leads.RemoveAll(l => l.At < now - DaysKept);
        _data.Starts.RemoveAll(s => s.At < now - DaysKept);
        foreach (var (n, _) in _data.FirstSeen.Where(f => f.Value < now - DaysKept).ToList()) _data.FirstSeen.Remove(n);
    }

    void Save()
    {
        if (_path is null) return;
        try
        {
            StateFile.Write(_path, _data, Json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The count is a log, never state: a write a scanner holds is lost, the next one lands.
        }
    }
}

internal sealed class StatsData
{
    public List<StatsSlot> Hours { get; set; } = [];
    public List<StatsSlot> Days { get; set; } = [];
    public List<MergeMark> Merges { get; set; } = [];
    public List<LeadMark> Leads { get; set; } = [];
    public List<HarnessStart> Starts { get; set; } = [];
    public Dictionary<int, DateTimeOffset> FirstSeen { get; set; } = [];
}

internal sealed class StatsSlot
{
    public DateTimeOffset At { get; set; }
    public Dictionary<string, Tally> Roles { get; set; } = [];
}

internal sealed class Tally
{
    public int Ok { get; set; }
    public int Failed { get; set; }
    public int Broke { get; set; }
    public int Findings { get; set; }
    public int[] Took { get; set; } = new int[JobStats.Edges.Length + 1];
    public int[] Waited { get; set; } = new int[JobStats.Edges.Length + 1];
    public List<int> Stories { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public int Jobs => Ok + Failed + Broke;
}

internal sealed record MergeMark(DateTimeOffset At, int Pr);

internal sealed record LeadMark(DateTimeOffset At, int Story, double Seconds);
