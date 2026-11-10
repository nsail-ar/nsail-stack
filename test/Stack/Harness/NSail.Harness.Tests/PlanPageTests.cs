// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Hosting;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// The status page shows a few rows of the plan and of what it holds; /plan has all of it, searched and
// a page at a time, so a long queue stays readable.
public class PlanPageTests
{
    readonly HarnessOptions _options = new() { Repositories = [new RepositoryOptions { Name = "acme/app" }] };

    static Plan PlanOf(int items, params string[] notes)
    {
        var work = Enumerable.Range(1, items)
            .Select(n => new WorkItem(WorkKind.Dev, n, null, n % 2 == 0 ? "qa" : "rest", "rest"))
            .ToList();
        return new Plan(work, [], notes);
    }

    static int CountRows(string html)
    {
        return html.Split("<td><span class=b>").Length - 1;
    }

    [Fact]
    public void TheStatusPageShowsTenPlanRowsAndCountsTheRest()
    {
        var html = new System.Text.StringBuilder();

        PlanPage.Summary(html, _options, PlanOf(60));

        var page = html.ToString();
        Assert.Equal(10, CountRows(page));
        Assert.Contains("Plan (60)", page);
        Assert.Contains("and 50 more: 25 rest · 25 qa", page);
        Assert.Contains("href=\"/plan\"", page);
    }

    [Fact]
    public void TheHeldStoriesReadAsOneLinePerStage()
    {
        var notes = Enumerable.Range(1, 17).Select(n => $"#{n}: stalled in QA, not Analyze — a hand moves it").Append("#99: locked").ToArray();

        var held = PlanPage.Held(notes);

        Assert.Equal(2, held.Count);
        Assert.Contains("#99: locked", held);
        Assert.Contains(held, line => line.StartsWith("17 stalled in QA", StringComparison.Ordinal) && line.Contains("and 7 more"));
    }

    [Fact]
    public void ThePlanPagePagesTwentyFiveAtATimeAndClampsThePage()
    {
        var plan = PlanOf(60);

        var first = PlanPage.List(_options, plan, null, null, null, null);
        var last = PlanPage.List(_options, plan, null, null, 99, null);

        Assert.Equal(25, CountRows(first));
        Assert.Contains("page 1 of 3", first);
        Assert.Contains("page 3 of 3", last);
        Assert.Equal(10, CountRows(last));
    }

    [Fact]
    public void ThePlanPageSearchesTheWorkAndFiltersByTier()
    {
        var plan = PlanOf(60);

        var byStory = PlanPage.List(_options, plan, "#42", null, null, null);
        var byTier = PlanPage.List(_options, plan, null, "qa", null, null);
        var none = PlanPage.List(_options, plan, "nothing like this", null, null, null);

        Assert.Equal(1, CountRows(byStory));
        Assert.Contains("1 item<", byStory);
        Assert.Contains("30 items", byTier);
        Assert.Contains("Nothing in the plan matches.", none);
    }

    [Fact]
    public void TheHeldNotesPageOnTheirOwnAndAreSearched()
    {
        var notes = Enumerable.Range(1, 60).Select(n => $"#{n}: stalled in QA, not Analyze").ToArray();
        var plan = PlanOf(3, notes);

        var second = PlanPage.List(_options, plan, null, null, null, 2);
        var found = PlanPage.List(_options, plan, "#37:", null, null, null);

        Assert.Contains("page 2 of 3", second);
        Assert.Contains("1 note<", found);
        Assert.Contains("#37", found);
    }

    [Fact]
    public void WithoutAPassThePlanPageSaysSo()
    {
        Assert.Contains("No pass has run yet.", PlanPage.List(_options, null, null, null, null, null));
    }
}
