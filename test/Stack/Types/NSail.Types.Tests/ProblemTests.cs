// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Types.Tests;

public sealed class ProblemTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        var code = "PROB001";
        var title = "Test Problem";
        var issues = new[]
        {
            new Issue("ERR001", "First issue"),
            new Issue("ERR002", "Second issue")
        };
        var status = 400;

        var problem = new Problem(code, title, issues, status);

        Assert.Equal(code, problem.Code);
        Assert.Equal(title, problem.Title);
        Assert.Equal(status, problem.Status);
        Assert.Equal(2, problem.Issues.Count);
        Assert.Equal("ERR001", problem.Issues[0].Code);
        Assert.Equal("ERR002", problem.Issues[1].Code);
    }

    [Fact]
    public void Constructor_WithoutStatus_SetsStatusToNull()
    {
        var code = "PROB002";
        var title = "Test Problem without status";
        var issues = new[] { new Issue("ERR001", "Issue") };

        var problem = new Problem(code, title, issues);

        Assert.Equal(code, problem.Code);
        Assert.Equal(title, problem.Title);
        Assert.Null(problem.Status);
        Assert.Single(problem.Issues);
    }

    [Fact]
    public void Constructor_WithEmptyIssues_CreatesEmptyIssuesList()
    {
        var code = "PROB003";
        var title = "Problem with no issues";
        var issues = Array.Empty<Issue>();

        var problem = new Problem(code, title, issues);

        Assert.Equal(code, problem.Code);
        Assert.Equal(title, problem.Title);
        Assert.Empty(problem.Issues);
    }

    [Fact]
    public void Constructor_WithNullStatus_SetsStatusToNull()
    {
        var code = "PROB004";
        var title = "Problem with explicit null status";
        var issues = new[] { new Issue("ERR001", "Issue") };

        var problem = new Problem(code, title, issues, null);

        Assert.Null(problem.Status);
    }

    [Fact]
    public void Issues_IsReadOnly_CannotBeModified()
    {
        var code = "PROB005";
        var title = "Test Problem";
        var issues = new[] { new Issue("ERR001", "Issue") };
        var problem = new Problem(code, title, issues);

        Assert.IsAssignableFrom<IReadOnlyList<Issue>>(problem.Issues);
    }

    [Fact]
    public void Issues_IsACopy_ModifyingSourceDoesNotAffectProblem()
    {
        var code = "PROB006";
        var title = "Test Problem";
        var issuesList = new List<Issue> { new Issue("ERR001", "Issue") };
        var problem = new Problem(code, title, issuesList);

        issuesList.Add(new Issue("ERR002", "Another issue"));

        Assert.Single(problem.Issues);
    }

    [Fact]
    public void ImplicitConversion_ToBusinessException_CreatesCorrectException()
    {
        var code = "PROB007";
        var title = "Test Problem";
        var issues = new[] { new Issue("ERR001", "Issue") };
        var status = 500;
        var problem = new Problem(code, title, issues, status);

        BusinessException exception = problem;

        Assert.Equal(code, exception.Code);
        Assert.Equal(title, exception.Title);
        Assert.Equal(title, exception.Message);
        Assert.Equal(status, exception.Status);
        Assert.Single(exception.Issues);
        Assert.Equal("ERR001", exception.Issues[0].Code);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(null)]
    public void ImplicitConversion_WithVariousStatuses_PreservesStatus(int? status)
    {
        var problem = new Problem("CODE", "Title", Array.Empty<Issue>(), status);

        BusinessException exception = problem;

        Assert.Equal(status, exception.Status);
    }

    [Fact]
    public void Unhandled_ReturnsUnknownProblem()
    {
        var problem = SystemProblem.Unhandled();

        Assert.Equal("Unknown", problem.Code);
        Assert.Equal("An unknown error occurred", problem.Title);
        Assert.Equal(500, problem.Status);
        var issue = Assert.Single(problem.Issues);
        Assert.Equal("Unknown", issue.Code);
        Assert.Equal("An unexpected and unknown error occurred", issue.Message);
    }
}
