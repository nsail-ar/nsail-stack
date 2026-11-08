// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using NSail.Harness.Hosting;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// The crew's count: each job that ends lands in its role's hour and day, its time in a histogram;
// what found nothing to do is not counted; a PR the reviewer landed closes its story's
// lead time; the file survives a restart and holds only its windows.
public class JobStatsTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 29, 15, 30, 0, TimeSpan.Zero);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    static void End(JobStats s, WorkKind kind, int? story, TimeSpan took, JobOutcome? outcome, DateTimeOffset? at = null, TimeSpan? waited = null)
    {
        var ended = at ?? Now;
        s.Ended(kind, story, ended - took, ended, waited, outcome);
    }

    [Fact]
    public void AJobLandsInItsRoleAndItsHour()
    {
        var s = new JobStats(null, Utc);
        End(s, WorkKind.Gate, 10, TimeSpan.FromMinutes(2), new JobOutcome("green"));
        End(s, WorkKind.Main, null, TimeSpan.FromMinutes(9), new JobOutcome("main green"));
        End(s, WorkKind.Review, 10, TimeSpan.FromMinutes(3), new JobOutcome("findings", Event: "Findings"));

        var view = s.View("24h", Now);

        Assert.Equal(24, view.Buckets.Count);
        var last = view.Buckets[^1];
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero), last.At);
        Assert.Equal(2, last.Jobs.Single(j => j.Role == "gate").Count);
        Assert.Equal(1, last.Jobs.Single(j => j.Role == "reviewer").Count);
        Assert.Equal(3, view.Totals.Jobs);
        Assert.Equal(1, view.Totals.ReviewsWithFindings);
    }

    [Fact]
    public void FailedAndBrokeAreToldApartAndQuietJobsAreNotCounted()
    {
        var s = new JobStats(null, Utc);
        End(s, WorkKind.Dev, 1, TimeSpan.FromMinutes(20), new JobOutcome("pushed"));
        End(s, WorkKind.Dev, 2, TimeSpan.FromMinutes(20), new JobOutcome("dead voyage", Failed: true));
        End(s, WorkKind.Dev, 3, TimeSpan.FromMinutes(1), null);
        End(s, WorkKind.Dev, 4, TimeSpan.FromSeconds(5), new JobOutcome("closed", Idle: true));
        End(s, WorkKind.Dev, 5, TimeSpan.FromSeconds(5), new JobOutcome("quota", QuotaUntil: Now.AddHours(2)));

        var dev = s.View("24h", Now).Roles.Single(r => r.Role == "dev");

        Assert.Equal((3, 1, 1, 1), (dev.Jobs, dev.Ok, dev.Failed, dev.Broke));
    }

    [Fact]
    public void TimesAreReadOffTheHistogramsEdges()
    {
        var s = new JobStats(null, Utc);
        for (var i = 0; i < 19; i++) End(s, WorkKind.Analyze, i, TimeSpan.FromSeconds(10), new JobOutcome("proposed"), waited: TimeSpan.FromSeconds(10));
        End(s, WorkKind.Analyze, 99, TimeSpan.FromMinutes(30), new JobOutcome("proposed"));

        var analyst = s.View("24h", Now).Roles.Single(r => r.Role == "analyst");

        Assert.Equal((15, 15, 15), (analyst.P50, analyst.P95, analyst.Wait));
        var slow = new JobStats(null, Utc);
        End(slow, WorkKind.Analyze, 1, TimeSpan.FromMinutes(30), new JobOutcome("proposed"));
        Assert.Equal(JobStats.Edges.First(e => e >= 1800), slow.View("24h", Now).Roles.Single(r => r.Role == "analyst").P50);
        Assert.Null(JobStats.Percentile(new int[JobStats.Edges.Length + 1], 0.5));
    }

    [Fact]
    public void TheJobsBeforeTheRangeAreItsTrend()
    {
        var s = new JobStats(null, Utc);
        End(s, WorkKind.Gate, 1, TimeSpan.FromMinutes(1), new JobOutcome("green"), Now.AddHours(-30));
        End(s, WorkKind.Gate, 1, TimeSpan.FromMinutes(1), new JobOutcome("green"), Now.AddHours(-26));
        End(s, WorkKind.Gate, 1, TimeSpan.FromMinutes(1), new JobOutcome("green"));

        var view = s.View("24h", Now);

        Assert.Equal((1, 2), (view.Totals.Jobs, view.Totals.Previous));
    }

    [Fact]
    public void DaysAreTheMachinesAndKeepAMonth()
    {
        var buenosAires = TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART");
        var s = new JobStats(null, buenosAires);
        // 01:30 UTC on the 29th is still the 28th in Buenos Aires.
        End(s, WorkKind.Dev, 1, TimeSpan.FromMinutes(5), new JobOutcome("pushed"), new DateTimeOffset(2026, 10, 29, 1, 30, 0, TimeSpan.Zero));
        End(s, WorkKind.Dev, 2, TimeSpan.FromMinutes(5), new JobOutcome("pushed"), Now.AddDays(-20));

        var week = s.View("7d", Now);
        var month = s.View("30d", Now);

        Assert.Equal(7, week.Buckets.Count);
        Assert.Equal(1, week.Buckets.Single(b => b.At == new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.FromHours(-3))).Jobs.Sum(j => j.Count));
        Assert.Equal(2, month.Totals.Jobs);
    }

    [Fact]
    public void AMergeClosesItsStorysLeadTime()
    {
        var s = new JobStats(null, Utc);
        s.Started(7, Now.AddHours(-3));
        s.Started(7, Now.AddHours(-1));
        s.Merged(70, 7, Now);
        s.Merged(71, null, Now);

        var view = s.View("24h", Now);

        Assert.Equal(2, view.Totals.Merged);
        Assert.Equal(TimeSpan.FromHours(3).TotalSeconds, view.Totals.LeadTime);
    }

    [Fact]
    public void AShipJobIsARelease()
    {
        Assert.Equal("release", JobStats.RoleOf(WorkKind.Ship));
    }

    [Fact]
    public void EveryKindIsNamedAndOnlyThePrsAndMainsGatesAreGates()
    {
        Assert.All(Enum.GetValues<WorkKind>(), k => Assert.Contains(JobStats.RoleOf(k), JobStats.Roles));
        Assert.Equal([WorkKind.Gate, WorkKind.Main], Enum.GetValues<WorkKind>().Where(k => JobStats.RoleOf(k) == "gate"));
    }

    [Fact]
    public void AWatchIsCountedAsAWatchNotAGate()
    {
        var s = new JobStats(null, Utc);
        End(s, WorkKind.Watch, null, TimeSpan.FromSeconds(20), null);

        var view = s.View("24h", Now);

        var watch = view.Roles.Single(r => r.Role == "watch");
        Assert.Equal((1, 1), (watch.Jobs, watch.Broke));
        Assert.Equal(0, view.Roles.Single(r => r.Role == "gate").Jobs);
        Assert.Equal(1, view.Buckets[^1].Jobs.Single(j => j.Role == "watch").Count);
        Assert.Equal(0, view.Buckets[^1].Jobs.Single(j => j.Role == "gate").Count);
    }

    [Fact]
    public void TheStatusPageDrawsTheWatchInItsLegendChartAndTable()
    {
        var s = new JobStats(null, Utc);
        End(s, WorkKind.Watch, null, TimeSpan.FromSeconds(20), null);
        var html = new StringBuilder();

        StatusPage.Stats(html, s.View("24h", Now));
        StatusPage.RoleTable(html, s.View("24h", Now));
        var quiet = new StringBuilder();
        StatusPage.RoleTable(quiet, new JobStats(null, Utc).View("24h", Now));

        var page = html.ToString();
        Assert.Contains("</span>Watch</span>", page);
        Assert.Contains("· Watch: 1</title>", page);
        Assert.Contains("</span>Watch</td><td class=n>1</td>", page);
        Assert.DoesNotContain("Watch", quiet.ToString());
    }

    [Fact]
    public void AGateTallyAlreadyOnDiskStaysAGate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"harness-stats-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "stats.json"),
                """{"Hours":[{"At":"2026-10-29T15:00:00+00:00","Roles":{"gate":{"Broke":9,"Ok":70}}}]}""");

            var view = new JobStats(root, Utc).View("24h", Now);

            Assert.Equal((79, 9), (view.Roles.Single(r => r.Role == "gate").Jobs, view.Roles.Single(r => r.Role == "gate").Broke));
            Assert.Equal(0, view.Roles.Single(r => r.Role == "watch").Jobs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DevRoundsAreCountedPerStory()
    {
        var s = new JobStats(null, Utc);
        End(s, WorkKind.Dev, 1, TimeSpan.FromMinutes(20), new JobOutcome("opened"));
        End(s, WorkKind.Dev, 1, TimeSpan.FromMinutes(10), new JobOutcome("pushed"));
        End(s, WorkKind.Dev, 2, TimeSpan.FromMinutes(20), new JobOutcome("opened"));

        Assert.Equal(1.5, s.View("24h", Now).Totals.DevRoundsPerStory);
    }

    [Fact]
    public void TheCountSurvivesARestartAndKeepsOnlyItsWindows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"harness-stats-{Guid.NewGuid():N}");
        try
        {
            var s = new JobStats(root, Utc);
            End(s, WorkKind.Gate, 1, TimeSpan.FromMinutes(1), new JobOutcome("green"), Now.AddDays(-100));
            End(s, WorkKind.Gate, 1, TimeSpan.FromMinutes(1), new JobOutcome("green"));
            s.Begun("d063496", Now);

            var again = new JobStats(root, Utc);

            Assert.Equal(1, again.View("30d", Now).Totals.Jobs);
            Assert.Equal("d063496", again.View("24h", Now).Starts.Single().Version);
            File.WriteAllText(Path.Combine(root, "stats.json"), "{ not json");
            Assert.Equal(0, new JobStats(root, Utc).View("24h", Now).Totals.Jobs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
    [Fact]
    public void DaysAfterADaylightChangeKeepTheirJobs()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var s = new JobStats(null, zone);
        // Clocks went back on 1 Nov 2026; a job on the 3rd is at a different offset than the 30th.
        var now = new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);
        End(s, WorkKind.Dev, 1, TimeSpan.FromMinutes(5), new JobOutcome("pushed"), new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero));
        End(s, WorkKind.Dev, 2, TimeSpan.FromMinutes(5), new JobOutcome("pushed"), now);

        var view = s.View("7d", now);

        Assert.Equal(7, view.Buckets.Count);
        Assert.Equal(2, view.Totals.Jobs);
        Assert.Equal(2, view.Buckets.Sum(b => b.Jobs.Sum(j => j.Count)));
        Assert.All(view.Buckets, b => Assert.Equal(TimeSpan.Zero, TimeZoneInfo.ConvertTime(b.At, zone).TimeOfDay));
    }

    [Fact]
    public void AFileThatCannotBeWrittenDoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"harness-stats-{Guid.NewGuid():N}");
        try
        {
            var s = new JobStats(root, Utc);
            Directory.CreateDirectory(Path.Combine(root, "stats.json"));

            s.Begun("d063496", Now);
            s.Merged(1, null, Now);
            End(s, WorkKind.Gate, 1, TimeSpan.FromMinutes(1), new JobOutcome("green"));

            Assert.Equal(1, s.View("24h", Now).Totals.Jobs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
