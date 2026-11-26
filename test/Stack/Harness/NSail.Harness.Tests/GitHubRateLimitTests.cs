// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
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

    static (GitHubClient Client, List<TimeSpan> Waits) Client(Script script, GitHubToken? token = null)
    {
        var waits = new List<TimeSpan>();
        var client = new GitHubClient(new HttpClient(script) { BaseAddress = new Uri("https://api.github.com/") }, token)
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
        Assert.Equal(2, script.Sent.Count);
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

    // An answer whose connection drops after its first bytes, as a chunked body cut mid-stream is.
    sealed class CutStream() : MemoryStream(Encoding.UTF8.GetBytes("""{"a":"""))
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            return read > 0 ? read : throw new IOException("The response ended prematurely.");
        }
    }

    static HttpResponseMessage Cut()
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CutStream()) };
    }

    [Fact]
    public async Task AReadWhoseAnswerIsCutMidBodyIsSentAgainAndReturnsTheNextAnswer()
    {
        var get = new Script(_ => Cut(), _ => Ok("""{"a":1}"""));
        Assert.Equal(1, (await Client(get).Client.Get("x")).GetProperty("a").GetInt32());
        Assert.Equal(2, get.Sent.Count);

        var query = new Script(_ => Cut(), _ => Cut(), _ => Ok("""{"data":{"a":1}}"""));
        Assert.Equal(1, (await Client(query).Client.GraphQl("query{a}")).GetProperty("a").GetInt32());
        Assert.Equal(3, query.Sent.Count);
    }

    [Fact]
    public async Task AWriteWhoseAnswerIsCutIsSentOnceAndThrowsTheProvidersFailureWithItsCall()
    {
        var post = new Script(_ => Cut(), _ => Ok());
        var e = await Assert.ThrowsAsync<ProviderTransportException>(() => Client(post).Client.Send(HttpMethod.Post, "repos/acme/app/statuses/abc", new { }));
        Assert.StartsWith("connection lost POST /repos/acme/app/statuses/abc: ", e.Message, StringComparison.Ordinal);
        Assert.Null(e.Status);
        Assert.IsType<HttpRequestException>(e.InnerException);
        Assert.Single(post.Sent);

        var mutation = new Script(_ => Cut(), _ => Ok("""{"data":{}}"""));
        await Assert.ThrowsAsync<ProviderTransportException>(() => Client(mutation).Client.GraphQl("mutation{a}"));
        Assert.Single(mutation.Sent);

        var upload = new Script(_ => Cut(), _ => Ok());
        await Assert.ThrowsAsync<ProviderTransportException>(() => Client(upload).Client.Upload("https://uploads.github.com/repos/acme/app/assets", new MemoryStream([1]), "image/png"));
        Assert.Single(upload.Sent);
    }

    [Fact]
    public async Task AReadCutOnEveryAttemptThrowsTheProvidersFailureWithItsCall()
    {
        var get = new Script(_ => Cut());
        var e = await Assert.ThrowsAsync<ProviderTransportException>(() => Client(get).Client.Get("repos/acme/app/pulls"));
        Assert.StartsWith("connection lost GET /repos/acme/app/pulls: ", e.Message, StringComparison.Ordinal);
        Assert.Null(e.Status);
        Assert.Equal(3, get.Sent.Count);

        var query = new Script(_ => Cut());
        await Assert.ThrowsAsync<ProviderTransportException>(() => Client(query).Client.GraphQl("query{a}"));
        Assert.Equal(3, query.Sent.Count);
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

        var malformed = new Script(_ => Ok(""), _ => Ok("""{"data":{}}"""));
        await Assert.ThrowsAsync<GitHubException>(() => Client(malformed).Client.GraphQl("mutation{a}"));
        Assert.Single(malformed.Sent);
    }

    // What GitHub answered on dev1 to a merged-PR read, with the token the reads before and after it used.
    static HttpResponseMessage BadCredentials()
    {
        return new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""
                {
                  "message": "Bad credentials",
                  "documentation_url": "https://docs.github.com/rest",
                  "status": "401"
                }
                """, Encoding.UTF8, "application/json"),
        };
    }

    public static TheoryData<string> Calls => ["GET", "DELETE", "query", "mutation"];

    static Task Call(GitHubClient client, string call)
    {
        return call switch
        {
            "GET" => client.Get("x"),
            "DELETE" => client.Send(HttpMethod.Delete, "x"),
            "query" => client.GraphQl("query{a}"),
            _ => client.GraphQl("mutation{a}"),
        };
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task ARefusedCredentialIsSentOnceMoreAfterAShortWait(string call)
    {
        var script = new Script(_ => BadCredentials(), _ => Ok("""{"data":{"a":1}}"""));
        var (client, waits) = Client(script);

        await Call(client, call);

        Assert.Equal(2, script.Sent.Count);
        Assert.InRange(Assert.Single(waits).TotalSeconds, 0.1, 5);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task ACredentialRefusedTwiceThrowsOnceNamingWhereTheTokenCameFrom(string call)
    {
        var script = new Script(_ => BadCredentials());
        var (client, _) = Client(script, new GitHubToken("github_pat_secret", "GH_TOKEN"));

        var e = await Assert.ThrowsAsync<GitHubException>(() => Call(client, call));

        Assert.Equal(HttpStatusCode.Unauthorized, e.HttpStatus);
        Assert.Null(e.RetryAfter);
        Assert.Equal(2, script.Sent.Count);
        Assert.Contains("Bad credentials", e.Message, StringComparison.Ordinal);
        Assert.EndsWith("the harness's GitHub token (GH_TOKEN) was refused twice", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("github_pat_secret", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATokenNamesItsSourceAndNeverItsValue()
    {
        var token = new GitHubToken("github_pat_secret", "GitHub:TokenFile /etc/harness/token");

        Assert.Equal("GitHub:TokenFile /etc/harness/token", token.ToString());
    }

    [Fact]
    public async Task ATokenIsReadFromTheFirstSettingThatHoldsOne()
    {
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, " from-file\n");

            Assert.Equal(new GitHubToken("inline", "GitHub:Token"), new GitHubOptions { Token = "inline", TokenFile = file }.ResolveToken());
            Assert.Equal(new GitHubToken("from-file", $"GitHub:TokenFile {file}"), new GitHubOptions { TokenFile = file }.ResolveToken());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableContent)]
    public async Task AnyOtherRefusalIsSentOnce(HttpStatusCode status)
    {
        var get = new Script(_ => new HttpResponseMessage(status) { Content = new StringContent("{}") }, _ => Ok());
        var e = await Assert.ThrowsAsync<GitHubException>(() => Client(get).Client.Get("x"));
        Assert.Equal(status, e.HttpStatus);
        Assert.Single(get.Sent);
        Assert.DoesNotContain("refused", e.Message, StringComparison.Ordinal);

        var delete = new Script(_ => new HttpResponseMessage(status) { Content = new StringContent("{}") }, _ => Ok());
        await Assert.ThrowsAsync<GitHubException>(() => Client(delete).Client.Send(HttpMethod.Delete, "x"));
        Assert.Single(delete.Sent);
    }

    [Fact]
    public async Task AFiveHundredReadIsStillSentThreeTimes()
    {
        var get = new Script(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("") });
        await Assert.ThrowsAsync<GitHubException>(() => Client(get).Client.Get("x"));
        Assert.Equal(3, get.Sent.Count);
    }

    [Fact]
    public async Task AMalformedGraphQlAnswerIsSentAgainAsAFiveHundredIs()
    {
        var script = new Script(_ => Ok(""), _ => Ok("""{"data":{"a":1}}"""));

        var data = await Client(script).Client.GraphQl("query{a}");

        Assert.Equal(1, data.GetProperty("a").GetInt32());
        Assert.Equal(2, script.Sent.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>unicorn</html>")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"errors":"boom"}""")]
    [InlineData("""{"errors":[1]}""")]
    [InlineData("""{"errors":[{"type":"X"}]}""")]
    public async Task AGraphQlAnswerMalformedOnEveryAttemptThrowsWithItsStatus(string body)
    {
        var script = new Script(_ => new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent(body) });

        var e = await Assert.ThrowsAsync<GitHubException>(() => Client(script).Client.GraphQl("query{a}"));

        Assert.Equal((int)HttpStatusCode.Accepted, e.Status);
        Assert.Equal(3, script.Sent.Count);
    }

    [Fact]
    public async Task AGraphQlErrorsAnswerIsThrownWithoutASecondSend()
    {
        var script = new Script(_ => Ok("""{"errors":[{"message":"nope"}]}"""), _ => Ok("""{"data":{}}"""));

        var e = await Assert.ThrowsAsync<GitHubException>(() => Client(script).Client.GraphQl("query{a}"));

        Assert.Equal("GraphQL: nope", e.Message);
        Assert.Single(script.Sent);
    }

    // A query GitHub cut on time is its 502 in a 200, sent again as one; a mutation's cut may have acted.
    [Fact]
    public async Task AGraphQlQueryCutOnTimeIsSentAgainAndAMutationIsNot()
    {
        const string cut = """{"data":null,"errors":[{"message":"We couldn't respond to your request in time. Sorry about that. Please try resubmitting your request and contact us if the problem persists."}]}""";
        var query = new Script(_ => Ok(cut), _ => Ok("""{"data":{"a":1}}"""));
        var mutation = new Script(_ => Ok(cut), _ => Ok("""{"data":{"a":1}}"""));

        Assert.Equal(1, (await Client(query).Client.GraphQl("query{a}")).GetProperty("a").GetInt32());
        await Assert.ThrowsAsync<GitHubException>(() => Client(mutation).Client.GraphQl("mutation{a}"));

        Assert.Equal(2, query.Sent.Count);
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

    // A call nothing answers, as HttpClient's own timeout ends it.
    sealed class Silent : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new UnreachableException();
        }
    }

    static GitHubClient Unanswered()
    {
        return new GitHubClient(new HttpClient(new Silent()) { BaseAddress = new Uri("https://api.github.com/"), Timeout = TimeSpan.FromMilliseconds(50) });
    }

    [Fact]
    public async Task ACallTheClientTimesOutThrowsTheProvidersFailureWithItsCall()
    {
        var e = await Assert.ThrowsAsync<ProviderTimeoutException>(() => Unanswered().Send(HttpMethod.Post, "repos/acme/app/statuses/abc", new { state = "success" }));

        Assert.StartsWith("timed out POST /repos/acme/app/statuses/abc: ", e.Message, StringComparison.Ordinal);
        Assert.Null(e.Status);
        Assert.IsType<TaskCanceledException>(e.InnerException);
        await Assert.ThrowsAsync<ProviderTimeoutException>(() => Unanswered().Get("repos/acme/app/pulls"));
        await Assert.ThrowsAsync<ProviderTimeoutException>(() => Unanswered().Upload("https://uploads.github.com/repos/acme/app/assets", new MemoryStream([1]), "image/png"));
    }

    [Fact]
    public async Task ACallItsCallerCancelsIsStillACancellation()
    {
        var client = new GitHubClient(new HttpClient(new Silent()) { BaseAddress = new Uri("https://api.github.com/") });
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var e = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Send(HttpMethod.Post, "repos/acme/app/statuses/abc", ct: stop.Token));

        Assert.IsNotAssignableFrom<ProviderException>(e);
    }
}
