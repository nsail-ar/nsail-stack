// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Claude;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

public class JobRulesTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TwoVoyageCommentsWithNoPushBetweenAreTwoDeadVoyages()
    {
        Assert.Equal(2, DevJob.Count([T0.AddHours(1), T0.AddHours(2)], [], T0));
    }

    [Fact]
    public void APushInsideASpanForgivesTheVoyagesBeforeIt()
    {
        // The voyage that ended at 3h pushed during its span: it is alive, the count starts over.
        Assert.Equal(0, DevJob.Count([T0.AddHours(1), T0.AddHours(3)], [T0.AddHours(2)], T0));
        // A push before the first voyage ended forgives only that one; the next, with no push, counts.
        Assert.Equal(1, DevJob.Count([T0.AddHours(1), T0.AddHours(3)], [T0.AddMinutes(30)], T0));
    }

    [Theory]
    [InlineData("claude:opus", "opus")]
    [InlineData("claude:fable", "fable")]
    [InlineData("codex:gpt-5", "sonnet")]
    [InlineData("area:shop", "sonnet")]
    public void TheDriversLabelPicksTheModelElseTheSeatsOwn(string label, string model)
    {
        Assert.Equal(model, JobContext.ModelOf(new HashSet<string> { label }, "claude", "sonnet"));
    }

    [Fact]
    public void AModelNameTheCliLacksIsMappedAndAnAliasPassesThrough()
    {
        var claude = new ClaudeOptions { Models = { ["fable"] = "claude-fable-5-1" } };

        Assert.Equal("claude-fable-5-1", claude.ModelOf("fable"));
        Assert.Equal("opus", claude.ModelOf("opus"));
    }

    [Fact]
    public void TheUsageWallReadsFromTheStreamNotFromProse()
    {
        var wall = """
            {"type":"assistant","message":"the thread says you hit your weekly limit"}
            {"type":"rate_limit_event","rate_limit_info":{"status":"rejected","resetsAt":1792950000}}
            {"type":"result","result":"stopped","is_error":true}
            """;
        var prose = """{"type":"result","result":"I read that someone hit your weekly limit, carrying on"}""";

        var signals = StreamSignals.Read(wall);
        Assert.True(signals.QuotaRefused);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792950000), signals.QuotaUntil);
        Assert.False(StreamSignals.Read(prose).QuotaRefused);
        Assert.True(StreamSignals.Read("""{"api_error":"usage_limit_reached"}""").QuotaRefused);
        Assert.True(StreamSignals.Read("""{"type":"result","api_error_status":429}""").QuotaRefused);
    }

    [Fact]
    public void ALockHolderReadsBackFromItsTag()
    {
        var holder = RefLock.Parse("""{"Machine":"box1","Job":"x","Kind":"Dev","At":"2026-10-26T10:00:00+00:00"}""");

        Assert.Equal("box1", holder.Machine);
        Assert.Equal("?", RefLock.Parse("not json").Machine);
    }

    [Fact]
    public void TextsFillTheirSlotsByName()
    {
        Assert.Equal("PR #5 — t", HarnessTexts.Fill(new HarnessTexts().Merged, ("pr", 5), ("title", "t")).Replace("Merged: ", ""));
    }
}
