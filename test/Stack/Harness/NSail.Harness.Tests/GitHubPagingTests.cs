// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

public class GitHubPagingTests
{
    sealed class Pages(int count, string array = "[]") : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["page"] ?? "1");
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(array, Encoding.UTF8, "application/json") };
            if (page < count)
            {
                response.Headers.Add("Link", $"<https://api.github.com/x?page={page + 1}>; rel=\"next\"");
            }
            return Task.FromResult(response);
        }
    }

    static GitHubClient Client(Pages pages)
    {
        return new GitHubClient(new HttpClient(pages) { BaseAddress = new Uri("https://api.github.com/") });
    }

    [Fact]
    public async Task TwentyOnePagesThrowInsteadOfReturningTwenty()
    {
        var pages = new Pages(21);
        await Assert.ThrowsAsync<GitHubPageLimitException>(() => Client(pages).GetAll("x"));
        Assert.Equal(20, pages.Requests.Count);
    }

    [Fact]
    public async Task TwentyPagesAnswerWhole()
    {
        var all = await Client(new Pages(20, "[1]")).GetAll("x");
        Assert.Equal(20, all.Count);
    }

    [Fact]
    public async Task ClosedSincePagesUntilNoNextPage()
    {
        var calls = 0;
        var handler = new Graph(() =>
        {
            calls++;
            var more = calls < 3;
            return "{\"data\":{\"repository\":{\"issues\":{\"pageInfo\":{\"hasNextPage\":" + (more ? "true" : "false") + ",\"endCursor\":\"c" + calls + "\"},\"nodes\":[{\"number\":" + calls + ",\"closedAt\":\"2026-11-07T00:00:00Z\",\"projectItems\":{\"nodes\":[]}}]}}}}";
        });
        var board = new GitHubBoard(new GitHubClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") }), "o/r", new GitHubBoardOptions { Repository = "o/r", Project = 1 });
        var closed = await board.ClosedSince(DateTimeOffset.Parse("2026-11-06T00:00:00Z"));
        Assert.Equal([1, 2, 3], closed.Select(c => c.Number));
    }

    sealed class Graph(Func<string> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(), Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ACiReadAsksForTheHeadSha()
    {
        var pages = new Pages(1, """{"workflow_runs":[]}""");
        var repo = new GitHubRepository(Client(pages), new RepositoryOptions { Name = "o/r" }, new StoryNames("app", ["story"]), "t");
        await repo.Ci("story/nsail-1", "abc123");
        Assert.Contains("head_sha=abc123", pages.Requests.Single());
    }

    sealed class Routed(Func<string, string> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(request.RequestUri.PathAndQuery), Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task AQuietPrIsReadByItsHeadShaWhenTheWideReadMissesIt()
    {
        var routed = new Routed(path => path.Contains("graphql")
            ? """{"data":{"repository":{"pullRequests":{"nodes":[{"number":7,"title":"t","headRefName":"b","headRefOid":"old777","isDraft":false,"mergeable":"MERGEABLE","updatedAt":"2026-01-01T00:00:00Z"}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}"""
            : """{"workflow_runs":[]}""");
        var client = new GitHubClient(new HttpClient(routed) { BaseAddress = new Uri("https://api.github.com/") });
        var repo = new GitHubRepository(client, new RepositoryOptions { Name = "o/r" }, new StoryNames("app", ["story"]), "t");
        await repo.OpenPrs();
        Assert.Contains(routed.Requests, r => r.Contains("head_sha=old777"));
    }

    // Answers 304 to the ETag it gave; each page names the next, as GitHub's Link header does.
    sealed class Tagged : HttpMessageHandler
    {
        public List<(string Path, HttpStatusCode Status)> Answered { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"] ?? "1");
            var tag = $"\"p{page}\"";
            var status = request.Headers.IfNoneMatch.Any(t => t.Tag == tag) ? HttpStatusCode.NotModified : HttpStatusCode.OK;
            Answered.Add((request.RequestUri.PathAndQuery, status));
            var response = new HttpResponseMessage(status) { Content = new StringContent(status == HttpStatusCode.OK ? $"[{page}]" : "", Encoding.UTF8, "application/json") };
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(tag);
            if (page < 2) response.Headers.Add("Link", "<https://api.github.com/x?page=2>; rel=\"next\"");
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task AnUnchangedListingIsAnsweredFromWhatItKeptPageByPage()
    {
        var tagged = new Tagged();
        var client = new GitHubClient(new HttpClient(tagged) { BaseAddress = new Uri("https://api.github.com/") });

        var first = await client.GetAll("x");
        var again = await client.GetAll("x");

        Assert.Equal([1, 2], first.Select(e => e.GetInt32()));
        Assert.Equal([1, 2], again.Select(e => e.GetInt32()));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotModified, HttpStatusCode.NotModified], tagged.Answered.Select(a => a.Status));
    }
}
