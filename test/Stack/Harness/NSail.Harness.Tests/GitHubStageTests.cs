// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Harness.GitHub;

namespace NSail.Harness.Tests;

public class GitHubStageTests
{
    static JsonElement Node(string items)
    {
        return JsonDocument.Parse("{\"projectItems\":{\"nodes\":[" + items + "]}}").RootElement.Clone();
    }

    const string InProject1 = """{"id":"i1","project":{"number":1},"fieldValueByName":{"name":"Merged"}}""";
    const string InProject1NoStatus = """{"id":"i1","project":{"number":1},"fieldValueByName":null}""";
    const string InProject7 = """{"id":"i7","project":{"number":7},"fieldValueByName":{"name":"QA"}}""";

    [Fact]
    public void TheStageIsTheProjectsStatus()
    {
        Assert.Equal("Merged", GitHubBoard.StageIn(Node(InProject1), 1));
    }

    [Fact]
    public void AnotherProjectsStatusIsNotTheBoards()
    {
        Assert.Equal("-", GitHubBoard.StageIn(Node(InProject7), 1));
        Assert.Equal("QA", GitHubBoard.StageIn(Node(InProject7 + "," + InProject1), 7));
    }

    [Fact]
    public void NoStatusOrNoItemIsADash()
    {
        Assert.Equal("-", GitHubBoard.StageIn(Node(InProject1NoStatus), 1));
        Assert.Equal("-", GitHubBoard.StageIn(Node(""), 1));
    }
}
