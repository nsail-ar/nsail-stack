// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A board, a repository or a lock refused or failed a call: what a job may catch and carry on from.
// Status is the provider's own code when it has one (an HTTP status for a web API); RateLimitRemaining
// is the calls its rate limit still allowed when it answered, when it says.
public class ProviderException(string message, int? status = null, long? rateLimitRemaining = null, TimeSpan? retryAfter = null, Exception? inner = null) : Exception(message, inner)
{
    // How long the provider asked callers to wait, when it limited the call.
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public int? Status { get; } = status;

    public long? RateLimitRemaining { get; } = rateLimitRemaining;
}
