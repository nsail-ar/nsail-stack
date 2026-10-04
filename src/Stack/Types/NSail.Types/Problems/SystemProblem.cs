// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public static class SystemProblem
{
    public static Problem InternalError()
    {
        return new(
            code: "InternalError",
            title: "An unexpected error occurred",
            issues: new[]
            {
                new Issue(
                    code: "InternalError",
                    message: "An unexpected error occurred while processing the request")
            },
            status: 500);
    }

    public static Problem Canceled()
    {
        return new(
            code: "Canceled",
            title: "Operation canceled",
            issues: Array.Empty<Issue>(),
            status: null);
    }

    public static Problem Unhandled()
    {
        return new(
            code: "Unknown",
            title: "An unknown error occurred",
            issues: new[]
            {
                new Issue(
                    code: "Unknown",
                    message: "An unexpected and unknown error occurred")
            },
            status: 500);
    }
}
