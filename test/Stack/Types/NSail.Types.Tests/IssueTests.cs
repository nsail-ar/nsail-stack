// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Types.Tests;

public sealed class IssueTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        var code = "ERR001";
        var message = "Test error message";
        var source = "TestSource";

        var issue = new Issue(code, message, source);

        Assert.Equal(code, issue.Code);
        Assert.Equal(message, issue.Message);
        Assert.Equal(source, issue.Source);
    }

    [Fact]
    public void Constructor_WithoutSource_SetsSourceToNull()
    {
        var code = "ERR002";
        var message = "Test error message without source";

        var issue = new Issue(code, message);

        Assert.Equal(code, issue.Code);
        Assert.Equal(message, issue.Message);
        Assert.Null(issue.Source);
    }

    [Fact]
    public void Constructor_WithNullSource_SetsSourceToNull()
    {
        var code = "ERR003";
        var message = "Test error message with explicit null source";

        var issue = new Issue(code, message, null);

        Assert.Equal(code, issue.Code);
        Assert.Equal(message, issue.Message);
        Assert.Null(issue.Source);
    }

    [Theory]
    [InlineData("", "Message", null)]
    [InlineData("CODE", "", null)]
    [InlineData("CODE", "Message", "")]
    public void Constructor_WithEmptyStrings_AcceptsEmptyValues(string code, string message, string? source)
    {
        var issue = new Issue(code, message, source);

        Assert.Equal(code, issue.Code);
        Assert.Equal(message, issue.Message);
        Assert.Equal(source, issue.Source);
    }
}
