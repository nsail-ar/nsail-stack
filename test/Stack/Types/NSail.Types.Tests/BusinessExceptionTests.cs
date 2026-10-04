// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Types.Tests;

public sealed class BusinessExceptionTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        var code = "BUS001";
        var title = "Business error occurred";
        var issues = new[]
        {
            new Issue("ERR001", "First issue"),
            new Issue("ERR002", "Second issue", "Field1")
        };
        var status = 400;

        var exception = new BusinessException(code, title, issues, status);

        Assert.Equal(code, exception.Code);
        Assert.Equal(title, exception.Title);
        Assert.Equal(title, exception.Message);
        Assert.Equal(status, exception.Status);
        Assert.Equal(2, exception.Issues.Count);
        Assert.Equal("ERR001", exception.Issues[0].Code);
        Assert.Equal("ERR002", exception.Issues[1].Code);
    }

    [Fact]
    public void Constructor_WithoutStatus_SetsStatusToNull()
    {
        var code = "BUS002";
        var title = "Business error without status";
        var issues = new[] { new Issue("ERR001", "Issue") };

        var exception = new BusinessException(code, title, issues);

        Assert.Equal(code, exception.Code);
        Assert.Equal(title, exception.Title);
        Assert.Null(exception.Status);
        Assert.Single(exception.Issues);
    }

    [Fact]
    public void Constructor_WithEmptyIssues_CreatesEmptyIssuesList()
    {
        var code = "BUS003";
        var title = "Business error with no issues";
        var issues = Array.Empty<Issue>();

        var exception = new BusinessException(code, title, issues);

        Assert.Empty(exception.Issues);
    }

    [Fact]
    public void Constructor_WithProblem_CreatesExceptionFromProblem()
    {
        var problem = new Problem(
            "PROB001",
            "Problem title",
            new[] { new Issue("ERR001", "Issue") },
            404);

        var exception = new BusinessException(problem);

        Assert.Equal(problem.Code, exception.Code);
        Assert.Equal(problem.Title, exception.Title);
        Assert.Equal(problem.Status, exception.Status);
        Assert.Single(exception.Issues);
        Assert.Equal(problem.Issues[0].Code, exception.Issues[0].Code);
    }

    [Fact]
    public void Constructor_WithProblemWithoutStatus_CreatesExceptionWithNullStatus()
    {
        var problem = new Problem(
            "PROB002",
            "Problem without status",
            new[] { new Issue("ERR001", "Issue") });

        var exception = new BusinessException(problem);

        Assert.Null(exception.Status);
    }

    [Fact]
    public void Issues_IsReadOnly_CannotBeModified()
    {
        var exception = new BusinessException(
            "BUS004",
            "Test",
            new[] { new Issue("ERR001", "Issue") });

        Assert.IsAssignableFrom<IReadOnlyList<Issue>>(exception.Issues);
    }

    [Fact]
    public void Issues_IsACopy_ModifyingSourceDoesNotAffectException()
    {
        var issuesList = new List<Issue> { new Issue("ERR001", "Issue") };
        var exception = new BusinessException("BUS005", "Test", issuesList);

        issuesList.Add(new Issue("ERR002", "Another issue"));

        Assert.Single(exception.Issues);
    }

    [Fact]
    public void ToProblem_CreatesProblemWithSameValues()
    {
        var code = "BUS006";
        var title = "Test exception";
        var issues = new[] { new Issue("ERR001", "Issue", "Source") };
        var status = 500;
        var exception = new BusinessException(code, title, issues, status);

        var problem = exception.ToProblem();

        Assert.Equal(code, problem.Code);
        Assert.Equal(title, problem.Title);
        Assert.Equal(status, problem.Status);
        Assert.Single(problem.Issues);
        Assert.Equal("ERR001", problem.Issues[0].Code);
        Assert.Equal("Issue", problem.Issues[0].Message);
        Assert.Equal("Source", problem.Issues[0].Source);
    }

    [Fact]
    public void ToProblem_WithNullStatus_CreatesProblemWithNullStatus()
    {
        var exception = new BusinessException(
            "BUS007",
            "Test",
            new[] { new Issue("ERR001", "Issue") });

        var problem = exception.ToProblem();

        Assert.Null(problem.Status);
    }

    [Fact]
    public void ToProblem_WithEmptyIssues_CreatesProblemWithEmptyIssues()
    {
        var exception = new BusinessException("BUS008", "Test", Array.Empty<Issue>());

        var problem = exception.ToProblem();

        Assert.Empty(problem.Issues);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(500)]
    [InlineData(503)]
    public void Constructor_WithVariousHttpStatusCodes_PreservesStatus(int status)
    {
        var exception = new BusinessException(
            "CODE",
            "Title",
            Array.Empty<Issue>(),
            status);

        Assert.Equal(status, exception.Status);
    }

    [Fact]
    public void RoundTrip_ExceptionToProblemToException_PreservesAllData()
    {
        var originalException = new BusinessException(
            "BUS009",
            "Original exception",
            new[]
            {
                new Issue("ERR001", "First issue", "Source1"),
                new Issue("ERR002", "Second issue")
            },
            422);

        var problem = originalException.ToProblem();
        var newException = new BusinessException(problem);

        Assert.Equal(originalException.Code, newException.Code);
        Assert.Equal(originalException.Title, newException.Title);
        Assert.Equal(originalException.Status, newException.Status);
        Assert.Equal(originalException.Issues.Count, newException.Issues.Count);
        Assert.Equal(originalException.Issues[0].Code, newException.Issues[0].Code);
        Assert.Equal(originalException.Issues[0].Message, newException.Issues[0].Message);
        Assert.Equal(originalException.Issues[0].Source, newException.Issues[0].Source);
    }

    [Fact]
    public void Message_ReturnsTitle()
    {
        var title = "This is the error title";
        var exception = new BusinessException("CODE", title, Array.Empty<Issue>());

        Assert.Equal(title, exception.Message);
    }
}
