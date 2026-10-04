// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public static class ExternalProblem
{
    public static Problem ServiceUnavailable(string service)
    {
        return new(
            code: "ExternalServiceUnavailable",
            title: "External service unavailable",
            issues: new[]
            {
                new Issue(
                    code: "ServiceUnavailable",
                    message: $"The external service {service} is not available",
                    source: service)
            },
            status: 503);
    }

    public static Problem Timeout(string service)
    {
        return new(
            code: "ExternalTimeout",
            title: "External service timeout",
            issues: new[]
            {
                new Issue(
                    code: "Timeout",
                    message: $"The external service {service} did not respond in time",
                    source: service)
            },
            status: 504);
    }
}
