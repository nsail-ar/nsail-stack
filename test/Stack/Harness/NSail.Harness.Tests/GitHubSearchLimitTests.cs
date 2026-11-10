// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

// A story's PRs are read through the search budget every job on the token shares: a refusal that
// names its reset within a minute is waited out once; anything else throws, never a partial list.
public class GitHubSearchLimitTests
{
    const string Pr = """{"number":7,"title":"app#3562: x","state":"closed","merged_at":"2026-11-08T09:00:00Z","merge_commit_sha":"m","user":{"login":"crew"},"head":{"ref":"story/app-3562","sha":"abc","repo":{"full_name":"o/r"}}}""";

    sealed class Search(params Func<HttpResponseMessage>[] searches) : HttpMessageHandler
    {
        int _next;

        public int Searched => _next;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/search/issues"))
            {
                var answer = searches[Math.Min(_next++, searches.Length - 1)]();
                answer.RequestMessage = request;
                return Task.FromResult(answer);
            }
            var body = path.EndsWith("/pulls") ? "[]" : Pr;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static HttpResponseMessage Found()
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"items":[{"number":7}]}""", Encoding.UTF8, "application/json") };
    }

    static HttpResponseMessage Exhausted(int? resetIn)
    {
        var r = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("""{"message":"API rate limit exceeded for user ID 1."}""") };
        r.Headers.Add("x-ratelimit-remaining", "0");
        r.Headers.Add("x-ratelimit-resource", "search");
        if (resetIn is { } s) r.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddSeconds(s).ToUnixTimeSeconds().ToString());
        return r;
    }

    static HttpResponseMessage Status(HttpStatusCode status)
    {
        return new HttpResponseMessage(status) { Content = new StringContent("{}") };
    }

    static (GitHubRepository Repo, List<TimeSpan> Waits) Repo(Search search)
    {
        var waits = new List<TimeSpan>();
        var gh = new GitHubClient(new HttpClient(search) { BaseAddress = new Uri("https://api.github.com/") })
        {
            Delay = (span, _) => { waits.Add(span); return Task.CompletedTask; },
        };
        return (new GitHubRepository(gh, new RepositoryOptions { Name = "o/r" }, new StoryNames("app", ["story"]), "t"), waits);
    }

    [Fact]
    public async Task ASearchRefusedForItsBudgetWaitsForTheResetAndAnswers()
    {
        var search = new Search(() => Exhausted(20), Found);
        var (repo, waits) = Repo(search);

        var prs = await repo.PrsOf(3562, "merged");

        Assert.Equal(7, Assert.Single(prs).Number);
        Assert.InRange(Assert.Single(waits).TotalSeconds, 15, 21);
        Assert.Equal(2, search.Searched);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARefusalThatIsNotALimitThrowsAtOnceWithItsStatus(HttpStatusCode status)
    {
        var search = new Search(() => Status(status), Found);
        var (repo, waits) = Repo(search);

        var e = await Assert.ThrowsAsync<GitHubException>(() => repo.PrsOf(3562, "merged"));

        Assert.Equal(status, e.HttpStatus);
        Assert.Empty(waits);
        Assert.Equal(1, search.Searched);
    }

    // A 5xx on a read is the provider's own failure, sent again by its own rule; one that stays throws its status.
    [Fact]
    public async Task A502ThrowsWithItsStatusAndNoLimitWait()
    {
        var search = new Search(() => Status(HttpStatusCode.BadGateway));
        var (repo, _) = Repo(search);

        var e = await Assert.ThrowsAsync<GitHubException>(() => repo.PrsOf(3562, "merged"));

        Assert.Equal(HttpStatusCode.BadGateway, e.HttpStatus);
        Assert.Null(e.RetryAfter);
    }

    [Fact]
    public async Task AnExhaustedBudgetThatNamesNoResetThrowsWithTheCallsLeft()
    {
        var search = new Search(() => Exhausted(null), Found);
        var (repo, waits) = Repo(search);

        var e = await Assert.ThrowsAsync<GitHubException>(() => repo.PrsOf(3562, "merged"));

        Assert.Equal(403, e.Status);
        Assert.Equal(0L, e.RateLimitRemaining);
        Assert.Empty(waits);
        Assert.Equal(1, search.Searched);
    }

    [Fact]
    public async Task AResetMoreThanAMinuteAwayIsNotWaited()
    {
        var search = new Search(() => Exhausted(600), Found);
        var (repo, waits) = Repo(search);

        var e = await Assert.ThrowsAsync<GitHubException>(() => repo.PrsOf(3562, "merged"));

        Assert.Empty(waits);
        Assert.Equal(1, search.Searched);
        Assert.NotNull(e.RetryAfter);
    }

    [Fact]
    public async Task ASecondRefusalAfterTheWaitThrowsAndReturnsNoPartialList()
    {
        var search = new Search(() => Exhausted(20), () => Exhausted(20), Found);
        var (repo, waits) = Repo(search);

        var e = await Assert.ThrowsAsync<GitHubException>(() => repo.PrsOf(3562, "merged"));

        Assert.Equal(403, e.Status);
        Assert.Equal(0L, e.RateLimitRemaining);
        Assert.Single(waits);
        Assert.Equal(2, search.Searched);
    }
}
