// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public static class SecurityProblem
{
    public static Problem Unauthorized()
    {
        return new(
            code: "Unauthorized",
            title: "Authentication required",
            issues: new[]
            {
                new Issue(
                    code: "Unauthorized",
                    message: "The user is not authenticated")
            },
            status: 401);
    }

    /// <summary>What a deny-by-default gate answers when no policy allowed the send. Which of
    /// the two refusals it is depends on the CALLER, not on the message: a caller with no
    /// session was refused because there is nobody to allow, and that is a different fact from
    /// "you, specifically, may not". A client that cannot tell them apart has no way to know
    /// its credential died — it keeps a signed-in screen answering refusals forever.</summary>
    public static Problem Denied(bool authenticated, string action)
    {
        return authenticated ? Forbidden(action) : Unauthorized();
    }

    /// <summary>What a door open to the whole internet answers a caller who is asking too
    /// often. It is not a refusal of the caller — it says nothing about whether they were
    /// allowed, or whether what they asked for exists — so it is the one verdict here that a
    /// second attempt, later, can turn into a different one.</summary>
    public static Problem TooManyRequests(string action)
    {
        return new(
            code: "TooManyRequests",
            title: "Too many requests",
            issues: new[]
            {
                new Issue(
                    code: "TooManyRequests",
                    message: $"Too many requests for {action}. Wait a moment and try again",
                    source: action)
            },
            status: 429);
    }

    public static Problem Forbidden(string action)
    {
        return new(
            code: "Forbidden",
            title: "Access denied",
            issues: new[]
            {
                new Issue(
                    code: "Forbidden",
                    message: $"You do not have permission to execute {action}",
                    source: action)
            },
            status: 403);
    }
}
