// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

public class GitHubSetStageTests
{
    const string Project = """{"data":{"repositoryOwner":{"projectV2":{"id":"P","field":{"id":"F","options":[{"id":"oReady","name":"Ready"},{"id":"oAnalyze","name":"Analyze"}]}}}}}""";
    const string NoItem = """{"data":{"repository":{"issue":{"id":"I","projectItems":{"nodes":[]}}}}}""";
    const string OnBoard = """{"data":{"repository":{"issue":{"id":"I","projectItems":{"nodes":[{"id":"existing","project":{"number":1},"fieldValueByName":null}]}}}}}""";
    const string OnAnotherBoard = """{"data":{"repository":{"issue":{"id":"I","projectItems":{"nodes":[{"id":"other","project":{"number":7},"fieldValueByName":null}]}}}}}""";
    const string Added = """{"data":{"addProjectV2ItemById":{"item":{"id":"added"}}}}""";
    const string AlreadyThere = """{"errors":[{"type":"UNPROCESSABLE","message":"Content already exists in this project"}]}""";
    const string Forbidden = """{"errors":[{"type":"FORBIDDEN","message":"Resource not accessible by integration"}]}""";
    const string Written = """{"data":{"updateProjectV2ItemFieldValue":{"projectV2Item":{"id":"x"}}}}""";

    // GraphQL answered by the operation each request names, in order per operation.
    sealed class Scripted(params (string Operation, string Answer)[] script) : HttpMessageHandler
    {
        readonly List<(string Operation, string Answer)> _left = [.. script];

        public List<(string Operation, JsonElement Variables)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var query = body.RootElement.GetProperty("query").GetString()!;
            var operation = new[] { "addProjectV2ItemById", "updateProjectV2ItemFieldValue", "projectV2(number", "projectItems" }.First(query.Contains);
            Sent.Add((operation, body.RootElement.GetProperty("variables").Clone()));
            var next = _left.FindIndex(s => s.Operation == operation);
            if (next < 0) throw new InvalidOperationException($"unscripted {operation}");
            var answer = _left[next].Answer;
            _left.RemoveAt(next);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    static GitHubBoard Board(Scripted handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubBoard(new GitHubClient(http), "o/r", new GitHubBoardOptions { Repository = "o/r", Project = 1 });
    }

    static string WrittenItem(Scripted handler)
    {
        var write = handler.Sent.Single(s => s.Operation == "updateProjectV2ItemFieldValue");
        return write.Variables.GetProperty("i").GetString()!;
    }

    [Fact]
    public async Task AnIssueTheProjectAddsMeanwhileGetsItsColumnOnThatItem()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", NoItem), ("addProjectV2ItemById", AlreadyThere),
            ("projectItems", OnBoard), ("updateProjectV2ItemFieldValue", Written));

        await Board(handler).SetStage(42, "Ready");

        Assert.Equal("existing", WrittenItem(handler));
        Assert.Equal("oReady", handler.Sent.Single(s => s.Operation == "updateProjectV2ItemFieldValue").Variables.GetProperty("o").GetString());
    }

    [Fact]
    public async Task AnIssueOffTheProjectIsAddedThenGetsItsColumn()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", OnAnotherBoard), ("addProjectV2ItemById", Added),
            ("updateProjectV2ItemFieldValue", Written));

        await Board(handler).SetStage(42, "Analyze");

        Assert.Equal("I", handler.Sent.Single(s => s.Operation == "addProjectV2ItemById").Variables.GetProperty("c").GetString());
        Assert.Equal("added", WrittenItem(handler));
    }

    [Fact]
    public async Task AnIssueOnTheProjectGetsItsColumnWithNoAdd()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", OnBoard), ("updateProjectV2ItemFieldValue", Written));

        await Board(handler).SetStage(42, "Ready");

        Assert.DoesNotContain(handler.Sent, s => s.Operation == "addProjectV2ItemById");
        Assert.Equal("existing", WrittenItem(handler));
    }

    [Fact]
    public async Task AnyOtherAddErrorFailsTheCall()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", NoItem), ("addProjectV2ItemById", Forbidden));

        var e = await Assert.ThrowsAsync<GitHubException>(() => Board(handler).SetStage(42, "Ready"));

        Assert.Contains("Resource not accessible", e.Message);
        Assert.DoesNotContain(handler.Sent, s => s.Operation == "updateProjectV2ItemFieldValue");
    }

    [Fact]
    public async Task AFieldWriteErrorFailsTheCall()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", OnBoard), ("updateProjectV2ItemFieldValue", Forbidden));

        await Assert.ThrowsAsync<GitHubException>(() => Board(handler).SetStage(42, "Ready"));
    }

    [Fact]
    public async Task TrySetStageAnswersFalseOnAnyOtherError()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", NoItem), ("addProjectV2ItemById", Forbidden));

        Assert.False(await Board(handler).TrySetStage(42, "Ready", NullLogger.Instance));
    }

    [Fact]
    public async Task AnAddThatClaimsTheItemButNoReadFindsItFailsTheCall()
    {
        var handler = new Scripted(("projectV2(number", Project), ("projectItems", NoItem), ("addProjectV2ItemById", AlreadyThere),
            ("projectItems", NoItem));

        var e = await Assert.ThrowsAsync<GitHubException>(() => Board(handler).SetStage(42, "Ready"));

        Assert.Contains("already exists", e.Message);
    }
}
