// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public enum NetworkFailureReason
{
    EmptyContent,
    DeserializationFailed,
    FallbackFromStatusCode
}

public static class NetworkProblem
{
    public static Problem RequestFailed(int? statusCode, NetworkFailureReason reason)
    {
        return new(
            code: "RequestFailed",
            title: "Unable to contact the server",
            issues: new[]
            {
                CreateStatusIssue(statusCode),
                CreateReasonIssue(reason)
            },
            status: statusCode);
    }

    static Issue CreateStatusIssue(int? statusCode)
    {
        return statusCode switch
        {
            null or 0 => new Issue(
                code: "NoResponse",
                message: "No response was received from the server."),
            _ => new Issue(
                code: "HttpStatus",
                message: $"The server responded with HTTP status code {statusCode}.")
        };
    }

    static Issue CreateReasonIssue(NetworkFailureReason reason)
    {
        return reason switch
        {
            NetworkFailureReason.EmptyContent => new Issue(
                code: "EmptyResponse",
                message: "The response body was empty."),
            NetworkFailureReason.DeserializationFailed => new Issue(
                code: "DeserializationFailed",
                message: "The response content could not be deserialized as a valid NSail problem."),
            NetworkFailureReason.FallbackFromStatusCode => new Issue(
                code: "StatusFallback",
                message: "The error was inferred from the HTTP status code."),
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };
    }
}
