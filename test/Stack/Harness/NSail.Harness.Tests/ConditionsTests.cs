// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Harness.Agents;
using NSail.Harness.Claude;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

public class ConditionsTests
{
    const string Argentina = "America/Argentina/Buenos_Aires";

    // 2026-10-28 is a Wednesday. Argentina is UTC-3 all year.
    static readonly DateTimeOffset Wednesday = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    static DateTimeOffset ArgentinaTime(int day, int hour, int minute = 0)
    {
        return new DateTimeOffset(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(-3));
    }

    static Readings At(DateTimeOffset now, double? cpu = null, double? memory = null, params UsageWindow[] usage)
    {
        return new Readings(now, _ => cpu, _ => memory, usage);
    }

    static readonly ScheduleCondition OfficeHours = new() { Start = "0 9 * * 1-5", End = "0 19 * * 1-5", TimeZone = Argentina };

    // --- Schedule ---

    [Theory]
    [InlineData(28, 10, true)]   // Wednesday morning
    [InlineData(28, 9, true)]    // the minute it opens
    [InlineData(28, 18, true)]   // the last hour
    [InlineData(28, 19, false)]  // the minute it closes
    [InlineData(28, 20, false)]  // evening
    [InlineData(28, 3, false)]   // night
    [InlineData(31, 11, false)]  // Saturday
    [InlineData(1, 11, false)]   // Sunday (November 1st)
    [InlineData(2, 11, true)]    // Monday (November 2nd)
    public void OfficeHoursAreOpenOnWeekdaysFromNineToSeven(int day, int hour, bool open)
    {
        var month = day < 28 ? 11 : 10;
        var now = new DateTimeOffset(2026, month, day, hour, 0, 0, TimeSpan.FromHours(-3));

        Assert.Equal(open, Conditions.IsOpen(OfficeHours, now));
    }

    [Theory]
    [InlineData(23, true)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(12, false)]
    [InlineData(21, false)]
    [InlineData(22, true)]
    public void ANightWindowCrossesMidnight(int hour, bool open)
    {
        var night = new ScheduleCondition { Start = "0 22 * * *", End = "0 6 * * *", TimeZone = Argentina };

        Assert.Equal(open, Conditions.IsOpen(night, ArgentinaTime(28, hour)));
    }

    [Fact]
    public void TheWindowIsReadInItsOwnTimeZone()
    {
        // 11:00 UTC is 08:00 in Argentina: before the office opens there, after it opened in UTC.
        var at = new DateTimeOffset(2026, 10, 28, 11, 0, 0, TimeSpan.Zero);
        var utc = new ScheduleCondition { Start = "0 9 * * 1-5", End = "0 19 * * 1-5", TimeZone = "UTC" };

        Assert.False(Conditions.IsOpen(OfficeHours, at));
        Assert.True(Conditions.IsOpen(utc, at));
    }

    // --- Load ---

    [Theory]
    [InlineData(90.0, 80.0, null, true)]
    [InlineData(80.0, 80.0, null, false)]   // Above is strict
    [InlineData(50.0, null, 60.0, true)]
    [InlineData(60.0, null, 60.0, false)]   // Below is strict
    [InlineData(70.0, 60.0, 80.0, true)]
    [InlineData(85.0, 60.0, 80.0, false)]
    public void ALoadConditionHoldsBetweenItsBounds(double value, double? above, double? below, bool holds)
    {
        var when = new WhenOptions { Cpu = new LoadCondition { Above = above, Below = below } };

        Assert.Equal(holds, Conditions.Hold(when, At(Wednesday, cpu: value), []));
    }

    [Fact]
    public void ALoadNotYetMeasuredStopsNoWorker()
    {
        var cpu = new WhenOptions { Cpu = new LoadCondition { Below = 1 } };
        var memory = new WhenOptions { Memory = new LoadCondition { Above = 99 } };

        Assert.True(Conditions.Hold(cpu, At(Wednesday), []));
        Assert.True(Conditions.Hold(memory, At(Wednesday), []));
    }

    [Fact]
    public void MemoryIsReadApartFromCpu()
    {
        var when = new WhenOptions { Memory = new LoadCondition { Above = 80 } };

        Assert.True(Conditions.Hold(when, At(Wednesday, cpu: 10, memory: 90), []));
        Assert.False(Conditions.Hold(when, At(Wednesday, cpu: 99, memory: 50), []));
    }

    [Fact]
    public void TheLoadIsAskedOverTheConditionsOwnWindow()
    {
        TimeSpan? asked = null;
        var readings = new Readings(Wednesday, over => { asked = over; return 50; }, _ => null, []);

        Conditions.Hold(new WhenOptions { Cpu = new LoadCondition { Above = 10, Minutes = 7 } }, readings, []);

        Assert.Equal(TimeSpan.FromMinutes(7), asked);
    }

    [Fact]
    public void AnAverageNeedsSamplesOverHalfItsWindow()
    {
        var samples = new LoadSamples(TimeSpan.FromMinutes(30));
        samples.Add(Wednesday, 40, 10);
        samples.Add(Wednesday.AddMinutes(1), 60, 30);

        Assert.Null(samples.Cpu(Wednesday.AddMinutes(1), TimeSpan.FromMinutes(5)));
        Assert.Equal(50, samples.Cpu(Wednesday.AddMinutes(1), TimeSpan.FromMinutes(2)));
        Assert.Equal(20, samples.Memory(Wednesday.AddMinutes(1), TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void AnAverageCountsOnlyItsWindow()
    {
        var samples = new LoadSamples(TimeSpan.FromMinutes(30));
        samples.Add(Wednesday, 100, 0);
        for (var m = 10; m <= 20; m++) samples.Add(Wednesday.AddMinutes(m), 20, 0);

        Assert.Equal(20, samples.Cpu(Wednesday.AddMinutes(20), TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void SamplesOlderThanTheKeepAreDropped()
    {
        var samples = new LoadSamples(TimeSpan.FromMinutes(10));
        samples.Add(Wednesday, 100, 0);
        samples.Add(Wednesday.AddMinutes(30), 10, 0);

        // The only sample left is the newest: no window has half its span covered.
        Assert.Null(samples.Cpu(Wednesday.AddMinutes(30), TimeSpan.FromMinutes(20)));
    }

    // --- Usage ---

    static UsageWindow Week(double utilization, DateTimeOffset resetsAt)
    {
        return new UsageWindow("seven_day", utilization, resetsAt, TimeSpan.FromDays(7));
    }

    [Fact]
    public void PaceIsTheShareUsedOverTheShareGoneBy()
    {
        // Half the week gone, half the tokens used: on pace.
        var half = Week(0.5, Wednesday.AddDays(3.5));
        // A day left, 30% used: far under pace.
        var under = Week(0.3, Wednesday.AddDays(1));
        // Two days in, 80% used: running out.
        var over = Week(0.8, Wednesday.AddDays(5));

        Assert.Equal(1, half.PaceAt(Wednesday), 3);
        Assert.Equal(0.35, under.PaceAt(Wednesday), 2);
        Assert.Equal(2.8, over.PaceAt(Wednesday), 2);
    }

    [Fact]
    public void AFreshWindowsPaceIsNotInfinite()
    {
        var fresh = Week(0.01, Wednesday.AddDays(7));

        Assert.Equal(1, fresh.PaceAt(Wednesday), 3);
    }

    [Theory]
    [InlineData(0.9, 80.0, null, true)]
    [InlineData(0.8, 80.0, null, false)]
    [InlineData(0.2, null, 50.0, true)]
    [InlineData(0.6, null, 50.0, false)]
    public void UsageIsReadInPercent(double utilization, double? above, double? below, bool holds)
    {
        var when = new WhenOptions { Usage = new UsageCondition { Above = above, Below = below } };

        Assert.Equal(holds, Conditions.Hold(when, At(Wednesday, usage: Week(utilization, Wednesday.AddDays(3))), []));
    }

    [Fact]
    public void SpendingTokensHoldsUnderPace()
    {
        // "Aprovechar tokens": a day left and 30% used — spend them.
        var spend = new WhenOptions { Usage = new UsageCondition { PaceBelow = 0.8 } };

        Assert.True(Conditions.Hold(spend, At(Wednesday, usage: Week(0.3, Wednesday.AddDays(1))), []));
        Assert.False(Conditions.Hold(spend, At(Wednesday, usage: Week(0.8, Wednesday.AddDays(1))), []));
    }

    [Fact]
    public void SavingTokensHoldsOverPace()
    {
        var save = new WhenOptions { Usage = new UsageCondition { PaceAbove = 1.2 } };

        Assert.True(Conditions.Hold(save, At(Wednesday, usage: Week(0.8, Wednesday.AddDays(5))), []));
        Assert.False(Conditions.Hold(save, At(Wednesday, usage: Week(0.5, Wednesday.AddDays(3.5))), []));
    }

    [Fact]
    public void UsageNamesItsWindow()
    {
        var fiveHours = new UsageWindow("five_hour", 0.95, Wednesday.AddHours(1), TimeSpan.FromHours(5));
        var when = new WhenOptions { Usage = new UsageCondition { Window = "five_hour", Above = 90 } };

        Assert.True(Conditions.Hold(when, At(Wednesday, usage: [Week(0.1, Wednesday.AddDays(3)), fiveHours]), []));
    }

    [Fact]
    public void UsageNeverReadOrPastItsResetStopsNoWorker()
    {
        // A fresh harness has read no quota yet: its agents must start, or nothing ever reads one.
        var when = new WhenOptions { Usage = new UsageCondition { Below = 50 } };

        Assert.True(Conditions.Hold(when, At(Wednesday), []));
        Assert.True(Conditions.Hold(when, At(Wednesday, usage: Week(0.9, Wednesday.AddMinutes(-1))), []));
    }

    // --- Several conditions ---

    [Fact]
    public void EveryConditionNamedMustHold()
    {
        var when = new WhenOptions { Schedule = OfficeHours, Cpu = new LoadCondition { Above = 80 } };

        Assert.True(Conditions.Hold(when, At(ArgentinaTime(28, 10), cpu: 90), []));
        Assert.False(Conditions.Hold(when, At(ArgentinaTime(28, 10), cpu: 50), []));
        Assert.False(Conditions.Hold(when, At(ArgentinaTime(28, 22), cpu: 90), []));
    }

    [Fact]
    public void NoConditionsAlwaysHolds()
    {
        Assert.True(Conditions.Hold(new WhenOptions(), At(Wednesday), []));
    }


    [Fact]
    public void AScheduleWithNoEndIsAFiringNotAWindow()
    {
        var cut = new ScheduleCondition { Start = "0 2 * * *", TimeZone = Argentina };

        Assert.True(Conditions.Hold(new WhenOptions { Schedule = cut }, At(ArgentinaTime(28, 15)), []));
        Assert.Equal(ArgentinaTime(28, 2), Conditions.LastFiring(cut, ArgentinaTime(28, 15)));
        Assert.Equal(ArgentinaTime(27, 2), Conditions.LastFiring(cut, ArgentinaTime(28, 1)));
    }

    [Fact]
    public void AWorkerWaitsForWhatItNeedsUp()
    {
        var when = new WhenOptions { Reachable = ["localhost:5434"] };

        Assert.False(Conditions.Hold(when, At(Wednesday), ["localhost:5434"]));
        Assert.True(Conditions.Hold(when, At(Wednesday), ["localhost:5432"]));
    }

    [Theory]
    [InlineData("not a cron", "0 19 * * *", "UTC", "Schedule.Start")]
    [InlineData("0 9 * * *", "61 * * * *", "UTC", "Schedule.End")]
    [InlineData("0 9 * * *", "0 19 * * *", "Mars/Olympus", "Schedule.TimeZone")]
    public void ABadScheduleIsNamed(string start, string end, string zone, string field)
    {
        var when = new WhenOptions { Schedule = new ScheduleCondition { Start = start, End = end, TimeZone = zone } };

        var problem = Assert.Single(Conditions.Problems("office", when));
        Assert.Contains("office", problem);
        Assert.Contains(field, problem);
    }

    [Fact]
    public void ALoadOrUsageWithNoBoundIsAProblem()
    {
        var when = new WhenOptions { Cpu = new LoadCondition(), Memory = new LoadCondition { Above = 1, Minutes = 0 }, Usage = new UsageCondition() };

        Assert.Equal(3, Conditions.Problems("w", when).Count());
    }

    // --- Build turns follow the limit ---

    [Fact]
    public async Task ARaisedBuildLimitGrantsTheWaitersAtOnce()
    {
        var leases = new BuildLeases(1);
        await leases.Acquire("a", 5, default);
        var waiting = leases.Acquire("b", 5, default);

        Assert.False(waiting.IsCompleted);
        leases.Limit = 2;

        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, leases.Held);
    }

    [Fact]
    public async Task ALoweredBuildLimitRevokesNothing()
    {
        var leases = new BuildLeases(3);
        await leases.Acquire("a", 5, default);
        await leases.Acquire("b", 5, default);
        await leases.Acquire("c", 5, default);

        leases.Limit = 1;
        var waiting = leases.Acquire("d", 5, default);

        Assert.Equal(3, leases.Held);
        Assert.False(waiting.IsCompleted);
    }

    // --- The agent's quota windows ---

    const string RateLimitEvent = """
        {"type":"rate_limit_event","rate_limit_info":{"status":"allowed","resetsAt":1793206200,"rateLimitType":"five_hour","unifiedWindows":{"five_hour":{"utilization":0.02,"resetsAt":1793206200,"status":"allowed"},"seven_day":{"utilization":0.71,"resetsAt":1793476800,"status":"allowed"},"mystery_window":{"utilization":0.5,"resetsAt":1793476800}}}}
        """;

    [Fact]
    public void TheStreamTellsTheQuotaWindows()
    {
        var usage = StreamSignals.Read(RateLimitEvent).Usage;

        Assert.Equal(2, usage.Count);
        var week = usage.Single(w => w.Name == "seven_day");
        Assert.Equal(0.71, week.Utilization);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793476800), week.ResetsAt);
        Assert.Equal(TimeSpan.FromDays(7), week.Length);
        Assert.Equal(TimeSpan.FromHours(5), usage.Single(w => w.Name == "five_hour").Length);
    }

    [Fact]
    public void TheNewestEventWins()
    {
        var later = RateLimitEvent.Replace("\"utilization\":0.71", "\"utilization\":0.75");

        var usage = StreamSignals.Read(RateLimitEvent + "\n" + later).Usage;

        Assert.Equal(0.75, usage.Single(w => w.Name == "seven_day").Utilization);
    }

    [Fact]
    public void AStreamWithNoEventTellsNoWindows()
    {
        Assert.Empty(StreamSignals.Read("""{"type":"result","result":"done"}""").Usage);
    }
}
