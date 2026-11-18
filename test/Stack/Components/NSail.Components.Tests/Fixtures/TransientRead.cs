// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A read refused in a way a second try can pass (503), so a fixture that measures the
/// Retry keeps one to press whatever Problem.IsRetryable comes to answer for a refusal.</summary>
public static class TransientRead
{
    public static Problem Unreachable(string message = "La lectura no llegó.", string? title = null)
    {
        return new Problem(
            code: "Unreachable",
            title: title ?? message,
            issues: [new Issue(code: "Unreachable", message: message)],
            status: 503);
    }
}
