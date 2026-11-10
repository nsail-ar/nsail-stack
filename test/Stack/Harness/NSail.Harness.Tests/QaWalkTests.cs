// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Harness.Board;
using NSail.Harness.Jobs;
using NSail.Harness.Workers;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// What the harness lays down for a QA walk: the product it is walked on, what QA already asked, the
// browser it walks with.
public class QaWalkTests
{
    static readonly Dictionary<string, ProductOptions> Products = new()
    {
        ["shop"] = new ProductOptions { Name = "Shop", Labels = ["area:shop"], Default = true, Tenant = "https://o.test" },
        ["clinic"] = new ProductOptions { Name = "Clinic", Labels = ["area:clinic"], Tenant = "https://t.test" },
    };

    [Fact]
    public void AStoryIsWalkedWhereItsLabelSaysElseOnTheDefault()
    {
        Assert.Equal("Clinic", QaJob.ProductOf(new HashSet<string> { "area:clinic", "p1" }, Products)!.Name);
        Assert.Equal("Shop", QaJob.ProductOf(new HashSet<string> { "area:sales" }, Products)!.Name);
    }

    [Fact]
    public async Task AStoryMarkedNoWalkClosesAsNotApplicableWithNoWalker()
    {
        var board = new MemoryBoard();
        board.Add(1, stage: Stages.Released, labels: [Labels.NoWalk]);
        var options = new HarnessOptions
        {
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Seats = { ["qa"] = new AgentOptions { Signature = "— QA", Model = "m", Prompt = "walk" } },
            Workers = { ["test"] = new WorkerOptions { Type = "Test", Seat = "qa" } },
        };
        var repo = new DevJobRunTests.FileRepository("nowhere");
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace>(),
            new NoAgent(), options, [], new DevJobRunTests.Lines(), new BuildLeases(1));
        var job = new QaJob(new WorkItem(WorkKind.Qa, 1, null, "rest", "rest", Worker: "test"), ctx, new JobHistory(Path.Combine(Path.GetTempPath(), "harness-nowalk-" + Guid.NewGuid().ToString("N"))));

        var outcome = await job.Run(1, "unwritten.log", CancellationToken.None);

        Assert.Equal("NotApplicable", outcome.Event);
        Assert.Contains("no-walk", outcome.Report);
        Assert.False(outcome.Failed);
    }

    [Fact]
    public void WhatWasAskedIsTheLastAskAndEverythingAfterIt()
    {
        var t = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        var comments = new List<StoryComment>
        {
            new(t, "first ask <!-- qa-asked: -->", "crew-bot"),
            new(t.AddHours(1), "second ask <!-- qa-asked: -->", "crew-bot"),
            new(t.AddHours(2), "done, it works", "ana"),
        };

        var asked = QaJob.Asked(comments, new HashSet<string> { "crew-bot" });

        Assert.DoesNotContain("first ask", asked);
        Assert.Contains("second ask", asked);
        Assert.Contains("done, it works", asked);
        Assert.Contains("--- ana ", asked);
        Assert.Contains("(untrusted)", asked);
        Assert.Contains("UNTRUSTED", asked);
        Assert.Equal("", QaJob.Asked([new StoryComment(t, "nothing asked", "x")], new HashSet<string>()));
    }

    static readonly DateTimeOffset AskedAt = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
    static readonly TimeSpan Recheck = TimeSpan.FromHours(6);

    static bool Quiet(string marker, DateTimeOffset? walked, DateTimeOffset now, params (double Hours, string Body)[] since)
    {
        var comments = new List<StoryComment> { new(AskedAt, $"the walk's words {marker}", "crew-bot") };
        comments.AddRange(since.Select(c => new StoryComment(AskedAt.AddHours(c.Hours), c.Body, "ana")));
        return QaJob.Quiet(comments, walked, now, Recheck);
    }

    [Fact]
    public void AnAskOfAPersonWaitsForWordsHoweverLong()
    {
        var walked = AskedAt.AddMinutes(-1);

        Assert.True(Quiet("<!-- qa-asked: -->", walked, AskedAt.AddDays(9)));
        Assert.True(Quiet("<!-- qa-asked: -->", null, AskedAt.AddDays(9)));
        Assert.False(Quiet("<!-- qa-asked: -->", walked, AskedAt.AddHours(2), (1, "done")));
    }

    [Fact]
    public void AnAnswerAlreadyWalkedIsNotNews()
    {
        Assert.True(Quiet("<!-- qa-asked: -->", AskedAt.AddHours(2), AskedAt.AddDays(9), (1, "what do you mean?")));
        Assert.False(Quiet("<!-- qa-asked: -->", AskedAt.AddHours(2), AskedAt.AddDays(9), (1, "what do you mean?"), (3, "done")));
    }

    [Fact]
    public void ADoubtIsTriedAgainAfterTheRecheckOrWhenSomebodyWrites()
    {
        var walked = AskedAt.AddMinutes(-1);

        Assert.True(Quiet(QaJob.DoubtMarker, walked, AskedAt.AddHours(5)));
        Assert.False(Quiet(QaJob.DoubtMarker, walked, AskedAt.AddHours(7)));
        Assert.False(Quiet(QaJob.DoubtMarker, walked, AskedAt.AddHours(2), (1, "the tenant is back")));
        Assert.False(Quiet(QaJob.DoubtMarker, null, AskedAt.AddHours(1)));
    }

    [Fact]
    public void ALabelWithNoMarkedCommentIsWalkedLikeADoubt()
    {
        var comments = new List<StoryComment> { new(AskedAt, "a hand's note", "ana") };

        Assert.True(QaJob.Quiet(comments, AskedAt.AddHours(1), AskedAt.AddHours(2), Recheck));
        Assert.False(QaJob.Quiet(comments, AskedAt.AddHours(1), AskedAt.AddHours(8), Recheck));
        Assert.False(QaJob.Quiet(comments, null, AskedAt.AddHours(2), Recheck));
    }

    [Fact]
    public void ADoubtsCommentCarriesItsOwnMarkerAndAnAsksDoesNot()
    {
        var on = WorkerTypes.Of(WorkKind.Qa)!.Defaults;

        Assert.Contains(QaJob.DoubtMarker, on["Doubt"].Single(a => a.Comment is not null).Comment);
        Assert.Contains(QaJob.AskMarker, on["Asked"].Single(a => a.Comment is not null).Comment);
        Assert.DoesNotContain(QaJob.DoubtMarker, on["Asked"].Single(a => a.Comment is not null).Comment);
    }

    [Fact]
    public void TheBrowserIsToldWhereItsShotsGoAndWhichOriginsItMayOpen()
    {
        var servers = new Dictionary<string, CommandOptions> { ["playwright"] = new() { Run = "npx", Args = ["@playwright/mcp", "--allowed-origins", "{origins}", "--output-dir", "{shots}"] } };

        var json = QaJob.McpConfig(servers, "/shots/7", Products.Values.Select(p => p.Tenant));

        Assert.Contains("\"command\":\"npx\"", json);
        Assert.Contains("https://o.test;https://t.test", json);
        Assert.Contains("/shots/7", json);
    }

    [Fact]
    public void ANotLiveWalkIsAskedAgainOnItsClockAndEveryOtherVerdictWaitsForTheCut()
    {
        Assert.False(QaJob.WaitsForTheCut(new JobOutcome("walked: NotLive", Event: "NotLive")));

        foreach (var verdict in new[] { "Ok", "Bug", "Asked", "Doubt", "NoVerdict" })
        {
            Assert.True(QaJob.WaitsForTheCut(new JobOutcome($"walked: {verdict}", Event: verdict)));
        }

        Assert.InRange(QaJob.NotLiveRecheck, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
    }

    [Fact]
    public void ATenantsBuildIsTheCommitItsMetadataCarriesElseItsTag()
    {
        Assert.Equal("9b206cccb3479b464369556b9f34f93119351d21", QaJob.BuildRef("1.0.0+9b206cccb3479b464369556b9f34f93119351d21", "admin/v"));
        Assert.Equal("admin/v1.0.0", QaJob.BuildRef("1.0.0", "admin/v"));
        Assert.Null(QaJob.BuildRef("dev", "admin/v"));
    }

    const string Missing = "Executable /home/leonardo/.cache/ms-playwright/chromium-1252/chrome-linux/chrome is not installed; expected executable at /home/leonardo/.cache/ms-playwright/chromium-1252/chrome-linux/chrome";

    static string Called(string id, string name)
    {
        return JsonSerializer.Serialize(new { type = "assistant", message = new { role = "assistant", content = new object[] { new { type = "tool_use", id, name, input = new { } } } } });
    }

    static string Answered(string id, string text)
    {
        return JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = new object[] { new { type = "tool_result", tool_use_id = id, content = new object[] { new { type = "text", text } } } } } });
    }

    static string Said(string text)
    {
        return JsonSerializer.Serialize(new { type = "assistant", message = new { role = "assistant", content = new object[] { new { type = "text", text } } } });
    }

    [Fact]
    public void AThreadQuotingTheMissingBrowserBesideABrowserThatAnsweredIsNotBrowserless()
    {
        var log = string.Join('\n',
            $"== walk nsail#2878 on m slot 1 — {Missing}",
            Called("r1", "Read"),
            Answered("r1", $"--- QA walk\nwalk void: {Missing}"),
            Called("b1", "mcp__playwright__browser_navigate"),
            Answered("b1", "### Page\n- Page URL: https://o.test/"),
            Said($"The thread quotes: {Missing}"));

        Assert.False(QaJob.Browserless(log, ["playwright"]));
    }

    [Fact]
    public void ABrowserToolsOwnResultSayingTheBrowserIsMissingIsBrowserlessForTheServersTheWorkerNames()
    {
        var log = string.Join('\n',
            Called("b1", "mcp__web__browser_navigate"),
            Answered("b1", $"Error: {Missing}"));

        Assert.True(QaJob.Browserless(log, ["web"]));
        Assert.False(QaJob.Browserless(log, ["playwright"]));
    }
}
