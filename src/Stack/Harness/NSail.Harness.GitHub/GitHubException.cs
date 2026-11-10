// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;

namespace NSail.Harness.GitHub;

public sealed class GitHubException(HttpStatusCode status, string message, long? rateLimitRemaining = null, TimeSpan? retryAfter = null)
    : ProviderException(message, (int)status, rateLimitRemaining, retryAfter)
{
    public HttpStatusCode HttpStatus { get; } = status;
}
