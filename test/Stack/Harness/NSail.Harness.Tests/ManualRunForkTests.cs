// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using NSail.Harness.Api;
using NSail.Harness.GitHub;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Problems;

namespace NSail.Harness.Tests;

// A job a hand sends takes the crew rule the planner applies: a PR whose head lives in another repository is never gated or reviewed.
public class ManualRunForkTests
{
    sealed class Pulls(string headRepo) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = $$$$"""{"title":"app#1: x","state":"open","head":{"ref":"story/app-1","sha":"abc","repo":{"full_name":"{{{{headRepo}}}}"}}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static HarnessHandler Handler(string headRepo)
    {
        var options = new RepositoryOptions { Name = "o/r" };
        var gh = new GitHubClient(new HttpClient(new Pulls(headRepo)) { BaseAddress = new Uri("https://api.github.com/") });
        var repo = new GitHubRepository(gh, options, new StoryNames("app", ["story"]), "t");
        return new HarnessHandler(null!, new HarnessOptions(), null!, null!, null!, null!, null!, new RepositorySet([(repo, options)]));
    }

    [Theory]
    [InlineData(WorkKind.Gate)]
    [InlineData(WorkKind.Review)]
    public async Task AForksPrIsNotGatedOrReviewedByHand(WorkKind kind)
    {
        var e = await Assert.ThrowsAsync<BusinessException>(() => Handler("fork/r").Handle(new RunJob { Kind = kind, Story = 1, Pr = 1 }));

        Assert.Equal("harness.run.fork", Assert.Single(e.Issues).Source);
    }

    [Fact]
    public async Task AnOwnHeadPassesTheCrewRuleAndReachesTheLoop()
    {
        await Assert.ThrowsAsync<NullReferenceException>(() => Handler("O/R").Handle(new RunJob { Kind = WorkKind.Gate, Story = 1, Pr = 1 }));
    }
}
