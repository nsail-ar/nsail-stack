// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public static class TenancyProblem
{
    /// <summary>What a tenant the install has suspended is answered, before authentication and
    /// before any endpoint. 503 and not 403: nothing is wrong with the caller or their
    /// credential, and nothing they can do changes the answer — the install is not serving this
    /// customer right now, which is exactly what the status says and what a monitor needs to
    /// read.</summary>
    public static Problem Suspended()
    {
        return new(
            code: "Suspended",
            title: "This account is suspended",
            issues: new[]
            {
                new Issue(
                    code: "Suspended",
                    message: "This account is suspended. Contact support to have it restored")
            },
            status: 503);
    }
}
