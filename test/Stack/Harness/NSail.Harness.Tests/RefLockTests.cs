// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using System.Text.Json;
using NSail.Harness.GitHub;
using NSail.Harness.Locks;

namespace NSail.Harness.Tests;

public class RefLockTests
{
    sealed class Fake(Func<HttpRequestMessage, (HttpStatusCode, string)> answer) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            var (status, body) = answer(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static RefLock Lock(Fake fake, string branch = "trunk")
    {
        return new RefLock(new GitHubClient(new HttpClient(fake) { BaseAddress = new Uri("https://api.github.com/") }), "o/r", "app", branch);
    }

    static string Tag(LockHolder h)
    {
        return JsonSerializer.Serialize(new { message = JsonSerializer.Serialize(h) });
    }

    static readonly LockHolder Mine = new("box", "job1", "Dev", DateTimeOffset.UtcNow, "aaaa");

    [Fact]
    public async Task ReleaseLeavesALockAnotherHolderTook()
    {
        var other = Mine with { Instance = "bbbb" };
        var fake = new Fake(r => r.Method == HttpMethod.Delete ? (HttpStatusCode.NoContent, "") :
            r.RequestUri!.AbsolutePath.Contains("/git/ref/") ? (HttpStatusCode.OK, """{"object":{"sha":"t1","type":"tag"}}""") : (HttpStatusCode.OK, Tag(other)));

        await Lock(fake).Release(7, Mine);

        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("DELETE"));
    }

    [Fact]
    public async Task ReleaseDeletesTheCallersOwnLock()
    {
        var fake = new Fake(r => r.Method == HttpMethod.Delete ? (HttpStatusCode.NoContent, "") :
            r.RequestUri!.AbsolutePath.Contains("/git/ref/") ? (HttpStatusCode.OK, """{"object":{"sha":"t2","type":"tag"}}""") : (HttpStatusCode.OK, Tag(Mine)));

        await Lock(fake).Release(7, Mine);

        Assert.Contains(fake.Calls, c => c.StartsWith("DELETE") && c.Contains("locks/app-7"));
    }

    [Fact]
    public async Task AcquireReadsTheConfiguredBranch()
    {
        var fake = new Fake(r => (HttpStatusCode.OK, """{"object":{"sha":"abc"},"sha":"t"}"""));

        await Lock(fake).TryAcquire(7, Mine);

        Assert.Contains(fake.Calls, c => c.Contains("/git/ref/heads/trunk"));
    }

    [Fact]
    public async Task HoldersListsOnlyTheExactKey()
    {
        var refs = """[{"ref":"refs/locks/app-7","object":{"sha":"t3","type":"tag"}},{"ref":"refs/locks/app-web-9","object":{"sha":"t4","type":"tag"}}]""";
        var fake = new Fake(r => r.RequestUri!.AbsolutePath.Contains("matching-refs") ? (HttpStatusCode.OK, refs) : (HttpStatusCode.OK, Tag(Mine)));

        var holders = await Lock(fake).Holders();

        Assert.Equal([7], holders.Keys);
    }
}
