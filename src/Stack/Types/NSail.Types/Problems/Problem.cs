// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public sealed class Problem
{
    public Problem(
        string code,
        string title,
        IReadOnlyList<Issue> issues,
        int? status = null)
    {
        Code = code;
        Title = title;
        Status = status;
        Issues = issues.ToArray();
    }

    public string Code { get; }

    public string Title { get; }

    public IReadOnlyList<Issue> Issues { get; }

    public int? Status { get; }
}
