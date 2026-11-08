// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Watching;

// Where the fleet's logs are read (a Watch worker's Source, by Name): the error lines logged in a
// window, newest first, at most `max` of them. The store's own columns stop here.
public interface ILogSource
{
    string Name { get; }

    Task<IReadOnlyList<LoggedError>> Errors(DateTimeOffset since, DateTimeOffset until, int max, CancellationToken ct = default);
}

// One error line: the service that logged it, the logger (Source) and the message template it
// logged, the exception it carried, the cell it ran in, and where to read it whole. A line that
// logged a provider's answer (a failed pass) also carries the call it made, the status it got and
// the calls its rate limit still allowed.
public sealed record LoggedError(
    DateTimeOffset At,
    string Service,
    string Source,
    string Template,
    string? ExceptionType,
    string? StackTrace,
    string Cell,
    string? TraceId,
    Uri? Link,
    string? ProviderCall = null,
    int? ProviderStatus = null,
    long? RateLimitRemaining = null);
