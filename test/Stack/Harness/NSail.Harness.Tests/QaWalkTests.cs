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

// What the harness lays down for a QA walk: the product it is walked on and the browser it walks with.
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
    public void ARulingWalkSendsTheStoryToAnalyzeWearingRulingWithTheReport()
    {
        var on = WorkerTypes.Of(WorkKind.Qa)!.Defaults;

        Assert.Contains(on["Ruling"], a => a.AddLabel == Labels.Ruling);
        Assert.Contains(on["Ruling"], a => a.MoveTo == Stages.Analyze);
        Assert.Contains("{report}", on["Ruling"].Single(a => a.Comment is not null).Comment);
        Assert.False(on.ContainsKey("Asked"));
        Assert.False(on.ContainsKey("Doubt"));
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

    // The QA column is what is being walked: a verdict that moves the story takes it out, and every other
    // ending (a doubt, a dead walker, a tenant found behind, the quota wall) hands it back to Released.
    [Fact]
    public void OnlyAVerdictThatMovesTheStoryLeavesItOutOfReleased()
    {
        foreach (var verdict in new[] { "Ok", "NotApplicable", "Bug", "BugAgain", "Ruling" })
        {
            Assert.True(QaJob.LeavesQa(new JobOutcome($"walked: {verdict}", Event: verdict)));
        }
        foreach (var verdict in new[] { "NotLive", "Doubt", "NoVerdict" })
        {
            Assert.False(QaJob.LeavesQa(new JobOutcome($"walked: {verdict}", Event: verdict)));
        }
        Assert.False(QaJob.LeavesQa(new JobOutcome("walk stopped at the quota wall", DateTimeOffset.UtcNow)));
        Assert.False(QaJob.LeavesQa(null));
    }

    [Fact]
    public void ANotLiveOrDoubtedWalkIsTriedAgainOnItsOwnClockAndEveryOtherVerdictWaitsForTheCut()
    {
        foreach (var verdict in new[] { "NotLive", "Doubt" })
        {
            Assert.False(QaJob.WaitsForTheCut(new JobOutcome($"walked: {verdict}", Event: verdict)));
        }

        foreach (var verdict in new[] { "Ok", "Bug", "Ruling", "NoVerdict" })
        {
            Assert.True(QaJob.WaitsForTheCut(new JobOutcome($"walked: {verdict}", Event: verdict)));
        }

        Assert.Equal(QaJob.NotLiveRecheck, QaJob.RecheckOf(new JobOutcome("walked: NotLive", Event: "NotLive")));
        Assert.Equal(QaJob.DoubtRecheck, QaJob.RecheckOf(new JobOutcome("walked: Doubt", Event: "Doubt")));
        Assert.Equal(QaJob.DoubtRecheck, QaJob.RecheckOf(new JobOutcome("walked: NotLive", Event: "NotLive", Report: "The tenant runs an older build.")));
        Assert.InRange(QaJob.NotLiveRecheck, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        Assert.True(QaJob.DoubtRecheck >= QaJob.NotLiveRecheck);
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
