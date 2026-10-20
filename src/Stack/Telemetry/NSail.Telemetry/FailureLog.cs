// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Telemetry;

/// <summary>Reads back out of the store the install's telemetry lands in the operations that
/// did not succeed. What it can answer with is what <see cref="MessageSpan"/> writes — an
/// operation name, a code, an exception's type and a trace id — so there is no value a
/// message carried for it to return and nothing for a caller to scrub. An install whose
/// store was handed no read credential registers no implementation at all, which is why
/// every caller resolves it as optional and says so in its own words.</summary>
public interface IFailureLog
{
    Task<IReadOnlyList<LoggedFailure>> Read(FailureLogQuery query, CancellationToken cancellationToken);
}

/// <summary>One person's own recent failures. The user is not optional: a door that could
/// ask for everybody's is a different read under a different permission, and leaving the
/// filter out of the seam is what keeps it from being made by accident.</summary>
public sealed record FailureLogQuery(Guid UserId, DateTimeOffset Since, int Max);

/// <summary>One operation that ended badly. <see cref="ProblemCode"/> is a refusal — a rule
/// answered — and <see cref="FailureType"/> is a crash; a failure is one or the other, and
/// neither is prose: the code is read into the rule's own sentence by whoever shows it, and
/// the type is a type's name.</summary>
public sealed record LoggedFailure(
    DateTimeOffset At,
    string Operation,
    string? ProblemCode,
    string? FailureType,
    string? TraceId);

/// <summary>The store did not answer. Its own sentence is never shown: what the reader of a
/// failure log reads is that the log is unavailable, which is all the person can act on.</summary>
public sealed class FailureLogException : Exception
{
    public FailureLogException()
    {
    }

    public FailureLogException(string message)
        : base(message)
    {
    }

    public FailureLogException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
