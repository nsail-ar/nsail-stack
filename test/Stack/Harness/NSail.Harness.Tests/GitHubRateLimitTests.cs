// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

// A limited or failing GitHub is waited out or thrown, never read as an empty answer.
public class GitHubRateLimitTests
{
    sealed class Script(params Func<HttpRequestMessage, HttpResponseMessage>[] answers) : HttpMessageHandler
    {
        int _next;

        public List<string> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return Task.FromResult(answers[Math.Min(_next++, answers.Length - 1)](request));
        }
    }

    static HttpResponseMessage Ok(string body = "{}")
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    static HttpResponseMessage Limited(HttpStatusCode status, int retryAfter)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfter));
        return response;
    }

    static (GitHubClient Client, List<TimeSpan> Waits) Client(Script script)
    {
        var waits = new List<TimeSpan>();
        var client = new GitHubClient(new HttpClient(script) { BaseAddress = new Uri("https://api.github.com/") })
        {
            Delay = (span, _) => { waits.Add(span); return Task.CompletedTask; },
        };
        return (client, waits);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ALimitedReadWaitsRetryAfterAndRetries(HttpStatusCode status)
    {
        var script = new Script(_ => Limited(status, 7), _ => Ok("""{"a":1}"""));
        var (client, waits) = Client(script);

        var answer = await client.Get("x");

        Assert.Equal(1, answer.GetProperty("a").GetInt32());
        Assert.Equal([TimeSpan.FromSeconds(7)], waits);
    }

    [Fact]
    public async Task AWriteIsSentAgainWithItsBodyAfterALimit()
    {
        var bodies = new List<string>();
        var script = new Script(r => { bodies.Add(r.Content!.ReadAsStringAsync().Result); return Limited(HttpStatusCode.TooManyRequests, 1); },
            r => { bodies.Add(r.Content!.ReadAsStringAsync().Result); return Ok(); });
        var (client, _) = Client(script);

        await client.Send(HttpMethod.Post, "x", new { a = 1 });

        Assert.Equal(2, bodies.Count);
        Assert.Equal(bodies[0], bodies[1]);
    }

    [Fact]
    public async Task APersistentLimitThrowsWithItsWait()
    {
        var script = new Script(_ => Limited(HttpStatusCode.TooManyRequests, 5));
        var (client, _) = Client(script);

        var e = await Assert.ThrowsAsync<GitHubException>(() => client.Get("x"));

        Assert.Equal(TimeSpan.FromSeconds(5), e.RetryAfter);
        Assert.Equal(3, script.Sent.Count);
    }

    [Fact]
    public async Task AWaitPastTheBoundIsNotSlept()
    {
        var script = new Script(_ => Limited(HttpStatusCode.TooManyRequests, 3600));
        var (client, waits) = Client(script);

        var e = await Assert.ThrowsAsync<GitHubException>(() => client.Get("x"));

        Assert.Empty(waits);
        Assert.Equal(TimeSpan.FromHours(1), e.RetryAfter);
        Assert.Single(script.Sent);
    }

    [Fact]
    public async Task AnExhaustedQuotaWaitsForItsReset()
    {
        var reset = DateTimeOffset.UtcNow.AddSeconds(20).ToUnixTimeSeconds();
        var script = new Script(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
            r.Headers.Add("x-ratelimit-remaining", "0");
            r.Headers.Add("x-ratelimit-reset", reset.ToString());
            return r;
        }, _ => Ok());
        var (client, waits) = Client(script);

        await client.Get("x");

        Assert.Single(waits);
        Assert.InRange(waits[0].TotalSeconds, 5, 21);
    }

    [Fact]
    public async Task AFiveHundredReadIsRetriedAndAWriteIsNot()
    {
        var get = new Script(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("") }, _ => Ok());
        await Client(get).Client.Get("x");
        Assert.Equal(2, get.Sent.Count);

        var post = new Script(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("") }, _ => Ok());
        await Assert.ThrowsAsync<GitHubException>(() => Client(post).Client.Send(HttpMethod.Post, "x", new { }));
        Assert.Single(post.Sent);
    }

    [Fact]
    public async Task AFiveHundredGraphQlQueryIsRetriedAndAMutationIsNot()
    {
        static HttpResponseMessage Bad()
        {
            return new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("") };
        }
        var query = new Script(_ => Bad(), _ => Ok("""{"data":{"a":1}}"""));
        await Client(query).Client.GraphQl("query{a}");
        Assert.Equal(2, query.Sent.Count);

        var mutation = new Script(_ => Bad(), _ => Ok("""{"data":{}}"""));
        await Assert.ThrowsAsync<GitHubException>(() => Client(mutation).Client.GraphQl("mutation{a}"));
        Assert.Single(mutation.Sent);
    }

    static GitHubRepository Repo(GitHubClient gh)
    {
        return new GitHubRepository(gh, new RepositoryOptions { Name = "o/r" }, new StoryNames("app", ["story"]), "t");
    }

    [Fact]
    public async Task PushTimesLetsAFailedReadPropagate()
    {
        var script = new Script(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("") });
        var (client, _) = Client(script);

        await Assert.ThrowsAsync<GitHubException>(() => Repo(client).PushTimes(1));
    }

    [Fact]
    public async Task PushTimesLetsAFailedCompareFromOneBranchPropagate()
    {
        var script = new Script(_ => Ok("""[{"ref":"refs/heads/story/app-1"}]"""), _ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("") });
        var (client, _) = Client(script);

        await Assert.ThrowsAsync<GitHubException>(() => Repo(client).PushTimes(1));
    }

    [Fact]
    public async Task PushTimesReadsANotFoundAsNoPushes()
    {
        var script = new Script(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("") });
        var (client, _) = Client(script);

        Assert.Empty(await Repo(client).PushTimes(1));
    }

    [Fact]
    public async Task PushTimesReadsANotFoundCompareAsNoPushes()
    {
        var script = new Script(_ => Ok("""[{"ref":"refs/heads/story/app-1"}]"""), _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("") });
        var (client, _) = Client(script);

        Assert.Empty(await Repo(client).PushTimes(1));
    }
}
