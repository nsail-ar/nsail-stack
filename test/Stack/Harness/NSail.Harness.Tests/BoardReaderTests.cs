// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

public class BoardReaderTests
{
    static readonly StoryNames Names = new("app", ["story", "stack"]);

    static List<JsonElement> Runs(string json)
    {
        return JsonDocument.Parse(json).RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    [Theory]
    [InlineData("app#123: fix(x): y", "whatever", 123)]
    [InlineData("chore: no story", "story/app-45", 45)]
    [InlineData("chore: no story", "story/app-45-half", 45)]
    [InlineData("chore: no story", "stack/app-7", 7)]
    [InlineData("chore: no story", "story/app-45x", null)]
    [InlineData("chore: no story", "rigging/dev2", null)]
    [InlineData("chore: no story", "other/app-45", null)]
    public void AStoryIsReadOffTheTitleFirstThenTheHead(string title, string head, int? story)
    {
        Assert.Equal(story, Names.StoryOf(title, head));
    }

    [Fact]
    public void AStoryIsNamedByItsProjectsKey()
    {
        var names = new StoryNames("oz", ["story"]);

        Assert.Equal("oz#5: fix it", names.PrTitle(5, "oz#5: fix it"));
        Assert.Equal("story/oz-5", names.Branch("story", 5));
        Assert.Null(names.StoryOf("app#5: x", "story/app-5"));
    }

    [Fact]
    public void TheNewestRunPerWorkflowDecides()
    {
        var runs = Runs("""
            [
              {"workflow_id":1,"run_started_at":"2026-10-25T10:00:00Z","status":"completed","conclusion":"failure"},
              {"workflow_id":1,"run_started_at":"2026-10-25T11:00:00Z","status":"completed","conclusion":"success"},
              {"workflow_id":2,"run_started_at":"2026-10-25T11:00:00Z","status":"completed","conclusion":"skipped"}
            ]
            """);

        Assert.Equal(CiState.Green, GitHubRepository.CiOf(runs));
    }

    [Fact]
    public void AGreenHeadIsGreenSinceItsLastWorkflowFinished()
    {
        var runs = Runs("""
            [
              {"workflow_id":1,"run_started_at":"2026-10-25T10:00:00Z","updated_at":"2026-10-25T12:00:00Z","status":"completed","conclusion":"failure"},
              {"workflow_id":1,"run_started_at":"2026-10-25T11:00:00Z","updated_at":"2026-10-25T11:20:00Z","status":"completed","conclusion":"success"},
              {"workflow_id":2,"run_started_at":"2026-10-25T11:00:00Z","updated_at":"2026-10-25T11:05:00Z","status":"completed","conclusion":"success"}
            ]
            """);

        Assert.Equal(DateTimeOffset.Parse("2026-10-25T11:20:00Z"), GitHubRepository.GreenAtOf(runs));
    }

    [Fact]
    public void ARunsFailedJobsAreReadWithTheirFirstFailedStep()
    {
        var jobs = Runs("""
            [
              {"id":11,"name":"build","conclusion":"failure","steps":[
                {"name":"Set up job","conclusion":"success"},
                {"name":"Test","conclusion":"failure"},
                {"name":"Pack","conclusion":"skipped"}]},
              {"id":12,"name":"docs","conclusion":"success","steps":[{"name":"Lint","conclusion":"success"}]},
              {"id":13,"name":"e2e","conclusion":"timed_out","steps":[]}
            ]
            """);

        Assert.Equal([(11L, "build", "Test"), (13L, "e2e", "timed_out")], GitHubRepository.FailedJobs(jobs).ToList());
    }

    [Fact]
    public void OneRedWorkflowMakesItRedAndOneInFlightMakesItPending()
    {
        Assert.Equal(CiState.Red, GitHubRepository.CiOf(Runs("""[{"workflow_id":1,"run_started_at":"a","status":"completed","conclusion":"cancelled"}]""")));
        Assert.Equal(CiState.Pending, GitHubRepository.CiOf(Runs("""
            [{"workflow_id":1,"run_started_at":"a","status":"completed","conclusion":"failure"},
             {"workflow_id":2,"run_started_at":"a","status":"in_progress","conclusion":null}]
            """)));
        Assert.Equal(CiState.Pending, GitHubRepository.CiOf([]));
    }

    [Fact]
    public void TheReviewersWordIsTheLastMarkAtThatSha()
    {
        var markers = new Markers("reviewer");
        var bodies = new[] { "x\n<!-- reviewer:findings:aaa -->", "y\n<!-- reviewer:clean:bbb -->", "z\n<!-- reviewer:findings:bbb -->" };

        Assert.Equal("findings", markers.ReviewAt(bodies, "bbb"));
        Assert.Equal("findings", markers.ReviewAt(bodies, "aaa"));
        Assert.Null(markers.ReviewAt(bodies, "ccc"));
        Assert.Equal(["aaa", "bbb"], markers.Findings(bodies));
        Assert.Null(new Markers("harness:review").ReviewAt(bodies, "bbb"));
    }

    [Fact]
    public void AnIssueParsesWithItsStageBlockersAndParent()
    {
        var node = JsonDocument.Parse("""
            {"number":5,"title":"t","milestone":null,
             "labels":{"nodes":[{"name":"p1"},{"name":"area:shop"}]},
             "subIssuesSummary":{"total":0,"percentCompleted":0},
             "parent":{"number":9,"subIssuesSummary":{"percentCompleted":40}},
             "blockedBy":{"nodes":[{"number":3,"state":"OPEN"},{"number":4,"state":"CLOSED"}]},
             "projectItems":{"nodes":[{"id":"i1","project":{"number":1},"fieldValueByName":{"name":"Ready"}}]}}
            """).RootElement;

        var issue = GitHubBoard.ParseIssue(node, 1);

        Assert.Equal("Ready", issue.Stage);
        Assert.Equal([3], issue.OpenBlockers);
        Assert.Equal(40, issue.ParentPercent);
        Assert.Equal(9, issue.Parent);
        Assert.True(issue.Has("p1"));
        Assert.False(issue.InVersion);
    }

    // A comment or a label moves the issue's updatedAt; a column move moves only its project item's.
    [Fact]
    public void AStoryWasTouchedAtTheLatestOfItsIssueAndItsItemOnTheProject()
    {
        static Issue Parse(string issueAt, string itemAt)
        {
            return GitHubBoard.ParseIssue(JsonDocument.Parse($$$"""
                {"number":5,"title":"t","milestone":null,"updatedAt":"{{{issueAt}}}",
                 "labels":{"nodes":[]},"subIssuesSummary":{"total":0,"percentCompleted":0},"parent":null,"blockedBy":{"nodes":[]},
                 "projectItems":{"nodes":[{"id":"i7","updatedAt":"2026-11-08T09:00:00Z","project":{"number":7},"fieldValueByName":null},
                                          {"id":"i1","updatedAt":"{{{itemAt}}}","project":{"number":1},"fieldValueByName":{"name":"In-Progress"}}]}}
                """).RootElement, 1);
        }

        Assert.Equal(DateTimeOffset.Parse("2026-11-08T07:48:00Z"), Parse("2026-11-08T07:48:00Z", "2026-11-08T07:30:00Z").TouchedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-11-08T07:47:30Z"), Parse("2026-11-08T07:30:00Z", "2026-11-08T07:47:30Z").TouchedAt);
    }
}
