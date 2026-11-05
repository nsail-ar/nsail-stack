// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Jobs;

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
    public void WhatWasAskedIsTheLastAskAndEverythingAfterIt()
    {
        var t = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        var comments = new List<StoryComment>
        {
            new(t, "first ask <!-- qa-asked: -->", "crew-bot"),
            new(t.AddHours(1), "second ask <!-- qa-asked: -->", "crew-bot"),
            new(t.AddHours(2), "done, it works", "ana"),
        };

        var asked = QaJob.Asked(comments);

        Assert.DoesNotContain("first ask", asked);
        Assert.Contains("second ask", asked);
        Assert.Contains("done, it works", asked);
        Assert.Equal("", QaJob.Asked([new StoryComment(t, "nothing asked", "x")]));
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
}
