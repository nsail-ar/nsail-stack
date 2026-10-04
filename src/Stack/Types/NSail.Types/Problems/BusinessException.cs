// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public sealed class BusinessException : Exception
{
    public BusinessException(string code, string title, IEnumerable<Issue> issues, int? status = null)
        : base(title)
    {
        Code = code;
        Title = title;
        Status = status;
        Issues = issues.ToArray();
    }

    public BusinessException(Problem problem)
        : this(problem.Code, problem.Title, problem.Issues, problem.Status)
    {
    }

    public string Code { get; }

    public string Title { get; }

    public int? Status { get; }

    public IReadOnlyList<Issue> Issues { get; }

    public Problem ToProblem()
    {
        return new(Code, Title, Issues, Status);
    }

    public static implicit operator BusinessException(Problem problem)
    {
        return new(problem);
    }
}
