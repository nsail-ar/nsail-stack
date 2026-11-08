// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

// The provider's own promises: merge never deletes a branch the base repository does not own, and a PR of
// another repository or an unlisted author is no story's PR.
public class GitHubMergeTests
{
    sealed class Recorder(string headRepo) : HttpMessageHandler
    {
        public List<string> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            var body = request.Method == HttpMethod.Get
                ? $$$$"""{"title":"app#1: x","state":"open","head":{"ref":"story/app-1","sha":"abc","repo":{"full_name":"{{{{headRepo}}}}"}}}"""
                : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static async Task<List<string>> Merge(string headRepo)
    {
        var handler = new Recorder(headRepo);
        var gh = new GitHubClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var repo = new GitHubRepository(gh, new RepositoryOptions { Name = "o/r" }, new StoryNames("app", ["story"]), "t");

        await repo.Merge(1);

        return handler.Sent;
    }

    [Fact]
    public async Task AnOwnHeadIsDeletedAfterTheMerge()
    {
        var sent = await Merge("o/r");

        Assert.Contains("DELETE /repos/o/r/git/refs/heads/story/app-1", sent);
    }

    [Fact]
    public async Task AForkHeadIsMergedAndNeverDeletedInTheBase()
    {
        var sent = await Merge("fork/r");

        Assert.Contains("PUT /repos/o/r/pulls/1/merge", sent);
        Assert.DoesNotContain(sent, s => s.StartsWith("DELETE"));
    }

    sealed class Reads(string headRepo, string user) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var pr = $$$$"""{"number":1,"title":"app#1: x","state":"open","user":{"login":"{{{{user}}}}"},"head":{"ref":"story/app-1","sha":"abc","repo":{"full_name":"{{{{headRepo}}}}"}}}""";
            var body = path.EndsWith("/search/issues") ? """{"items":[{"number":1}]}"""
                : path.EndsWith("/pulls") ? $"[{pr}]"
                : pr;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static async Task<int> Found(string headRepo, string user)
    {
        var gh = new GitHubClient(new HttpClient(new Reads(headRepo, user)) { BaseAddress = new Uri("https://api.github.com/") });
        var options = new RepositoryOptions { Name = "o/r", Authors = ["crew"] };
        var repo = new GitHubRepository(gh, options, new StoryNames("app", ["story"]), "t");

        return (await repo.PrsOf(1, "open")).Count;
    }

    [Fact]
    public async Task AnOwnPrByAListedAuthorIsTheStorys()
    {
        Assert.Equal(1, await Found("o/r", "crew"));
    }

    [Fact]
    public async Task AnOwnPrByAnUnlistedAuthorIsNotTheStorys()
    {
        Assert.Equal(0, await Found("o/r", "stranger"));
    }

    [Fact]
    public async Task AForkPrIsNotTheStorys()
    {
        Assert.Equal(0, await Found("fork/r", "crew"));
    }
}
