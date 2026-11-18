// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    /// <summary>Whether a second attempt can be answered otherwise: no response, a 5xx, 408 or
    /// 429. Every other 4xx is a refusal for good, and so is the tenant's Suspended, a 503 no
    /// retry changes.</summary>
    public bool IsRetryable()
    {
        return Status switch
        {
            null or 408 or 429 => true,
            >= 500 => Code != TenancyProblem.SuspendedCode,
            _ => false,
        };
    }
}
