// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Hosting;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

// /jobs lists what the machine remembers, filtered and a page at a time; /jobs/{id} is one job and
// its log; a record from before the harness kept its result is read off its outcome.
public class JobsPageTests : IDisposable
{
    static readonly DateTimeOffset Start = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
    readonly string _root = Path.Combine(Path.GetTempPath(), "jobs-page-" + Guid.NewGuid().ToString("N"));
    readonly HarnessOptions _options = new() { Repositories = [new RepositoryOptions { Name = "acme/app" }, new RepositoryOptions { Name = "acme/framework", Label = "area:stack" }] };

    public JobsPageTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    JobRecord Job(int n, string work, int? story, string? outcome, string? result, bool ended = true)
    {
        var log = Path.Combine(_root, $"{n}.log");
        File.WriteAllText(log, string.Join('\n', Enumerable.Range(1, 300).Select(i => $"line {i}")));
        var started = Start.AddMinutes(n);
        return new JobRecord($"job-{n}", work, story, started, ended ? started.AddMinutes(2) : null, outcome, log, result);
    }

    JobHistory History(params JobRecord[] jobs)
    {
        var history = new JobHistory(_root);
        foreach (var j in jobs) history.Record(j);
        return history;
    }

    [Fact]
    public void TheListFiltersByRoleResultAndStory()
    {
        var history = History(
            Job(1, "dev #5", 5, "pushed", "ok"),
            Job(2, "review #5", 5, "findings", "failed"),
            Job(3, "dev #6", 6, "failed: boom", "broke"),
            Job(4, "dev #5", 5, "pushed", "ok"));

        var byRole = JobsPage.List(_options, history, "dev", null, null, null);
        var byResult = JobsPage.List(_options, history, null, "broke", null, null);
        var byStory = JobsPage.List(_options, history, null, null, 6, null);
        var none = JobsPage.List(_options, history, "qa", null, null, null);

        Assert.Contains("3 jobs", byRole);
        Assert.DoesNotContain("/jobs/job-2\"", byRole);
        Assert.Contains("1 job<", byResult);
        Assert.Contains("/jobs/job-3\"", byResult);
        Assert.Contains("1 job<", byStory);
        Assert.Contains("/jobs/job-3\"", byStory);
        Assert.Contains("No job matches.", none);
    }

    [Fact]
    public void TheListSearchesTheWorkAndTheOutcome()
    {
        var history = History(
            Job(1, "dev #5", 5, "pushed the branch", "ok"),
            Job(2, "review #7", 7, "findings: a missing test", "failed"),
            Job(3, "dev #8", 8, "failed: boom", "broke"));

        var byWork = JobsPage.List(_options, history, null, null, null, null, "review");
        var byOutcome = JobsPage.List(_options, history, null, null, null, null, "missing test");
        var none = JobsPage.List(_options, history, null, null, null, null, "zzz");

        Assert.Contains("1 job<", byWork);
        Assert.Contains("/jobs/job-2\"", byWork);
        Assert.Contains("/jobs/job-2\"", byOutcome);
        Assert.Contains("No job matches.", none);
    }

    [Fact]
    public void TheListPagesTwentyFiveAtATimeAndClampsThePage()
    {
        var history = History([.. Enumerable.Range(1, 60).Select(n => Job(n, "dev #5", 5, "pushed", "ok"))]);

        var first = JobsPage.List(_options, history, null, null, null, null);
        var third = JobsPage.List(_options, history, null, null, null, 3);
        var beyond = JobsPage.List(_options, history, null, null, null, 99);
        var before = JobsPage.List(_options, history, null, null, null, -4);

        Assert.Contains("page 1 of 3", first);
        Assert.Equal(25, CountRows(first));
        Assert.Contains("/jobs/job-60\"", first);
        Assert.Equal(10, CountRows(third));
        Assert.Contains("page 3 of 3", beyond);
        Assert.Contains("page 1 of 3", before);
    }

    [Fact]
    public void AJobsPageShowsItsResultStoryTimesOutcomeAndLog()
    {
        var history = History(Job(1, "dev #5", 5, "pushed the branch", "ok"));

        var html = JobsPage.One(_options, history, "job-1", null);

        Assert.NotNull(html);
        Assert.Contains("<th>Result</th><td><span class=\"b ok\">ok</span>", html);
        Assert.Contains("#5", html);
        Assert.Contains("/jobs?story=5", html);
        Assert.Contains("<th>Started</th>", html);
        Assert.Contains("<th>Took</th>", html);
        Assert.Contains("pushed the branch", html);
        Assert.Contains("line 300", html);
        Assert.DoesNotContain("line 99<", html);
        Assert.Contains("line 99", JobsPage.One(_options, history, "job-1", 0)!);
    }

    [Fact]
    public void AnUnknownJobIsNothing()
    {
        Assert.Null(JobsPage.One(_options, History(Job(1, "dev #5", 5, "x", "ok")), "job-9", null));
    }

    [Fact]
    public void ARecordWithoutAResultIsReadOffItsOutcome()
    {
        Assert.Equal("broke", JobsPage.RowOf(Job(1, "dev #5", 5, "failed: boom", null)).Result);
        Assert.Equal("failed", JobsPage.RowOf(Job(2, "gate #5", 5, "red: build", null)).Result);
        Assert.Equal("failed", JobsPage.RowOf(Job(3, "gate", null, "main red", null)).Result);
        Assert.Equal("ok", JobsPage.RowOf(Job(4, "dev #5", 5, "pushed", null)).Result);
        Assert.Equal("running", JobsPage.RowOf(Job(5, "dev #5", 5, null, null, ended: false)).Result);
        Assert.Equal("quiet", JobsPage.RowOf(Job(6, "dev #5", 5, "x", "quiet")).Result);
    }

    [Fact]
    public void AnotherRepositorysPrLinksToItsOwnRepository()
    {
        var html = JobsPage.List(_options, History(Job(1, "review framework PR #130 (story app#5)", 5, "clean", "ok")), null, null, null, null);

        Assert.Contains("framework PR <a href=\"https://github.com/acme/framework/issues/130\">#130</a>", html);
        Assert.Contains("<a href=\"https://github.com/acme/app/issues/5\">#5</a>", html);
    }

    static int CountRows(string html)
    {
        return html.Split("<td class=when>").Length - 1;
    }
}
