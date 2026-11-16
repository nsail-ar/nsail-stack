// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;

namespace NSail.Harness.GitHub;

// How a GraphQL read answered 2xx failed: a body that is not a GraphQL answer, or a well-formed
// `errors` answer. With the two below, what a failed pass's record names in place of GitHub's text.
public enum GraphQlFailure
{
    NotAnAnswer,
    Errors,
}

// The first error's `type`, spelled as GitHub sends it: None when it sent none, Unknown for one not listed.
public enum GraphQlErrorType
{
    None,
    Unknown,
    NOT_FOUND,
    FORBIDDEN,
    INSUFFICIENT_SCOPES,
    RATE_LIMITED,
    MAX_NODE_LIMIT_EXCEEDED,
    EXCESSIVE_PAGINATION,
    UNPROCESSABLE,
    INTERNAL,
    SERVICE_UNAVAILABLE,
    TIMEOUT,
}

// Whether the first error's message is one GitHub sends for a query it cut on time.
public enum GraphQlMessage
{
    Timeout,
    Unrecognized,
}

static class GraphQlFailures
{
    static readonly string[] TimeoutMessages =
    [
        "Something went wrong while executing your query. This may be the result of a timeout",
        "We couldn't respond to your request in time",
    ];

    public static IReadOnlyList<KeyValuePair<string, Enum>> NotAnAnswer { get; } = [new(nameof(GraphQlFailure), GraphQlFailure.NotAnAnswer)];

    public static IReadOnlyList<KeyValuePair<string, Enum>> Errors(JsonElement first)
    {
        var message = first.GetProperty("message").GetString() ?? "";
        return
        [
            new(nameof(GraphQlFailure), GraphQlFailure.Errors),
            new(nameof(GraphQlErrorType), TypeOf(first)),
            new(nameof(GraphQlMessage), TimeoutMessages.Any(t => message.StartsWith(t, StringComparison.Ordinal)) ? GraphQlMessage.Timeout : GraphQlMessage.Unrecognized),
        ];
    }

    static GraphQlErrorType TypeOf(JsonElement error)
    {
        if (!error.TryGetProperty("type", out var type) || type.ValueKind is JsonValueKind.Null) return GraphQlErrorType.None;
        var text = type.ValueKind == JsonValueKind.String ? type.GetString() : null;
        // TryParse also reads a number or another case; only a member's own name is GitHub's type.
        return Enum.TryParse<GraphQlErrorType>(text, out var known) && known > GraphQlErrorType.Unknown && known.ToString() == text
            ? known
            : GraphQlErrorType.Unknown;
    }
}
