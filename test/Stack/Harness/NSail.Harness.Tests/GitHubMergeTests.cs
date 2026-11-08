// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

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
}
