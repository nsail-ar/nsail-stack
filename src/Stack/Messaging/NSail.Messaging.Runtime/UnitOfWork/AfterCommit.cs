// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;

namespace NSail.Messaging.Runtime.UnitOfWork;

/// <summary>Work a handler hands to the operation's own commit: registered while the handler
/// runs, invoked by the outermost send once every store has committed, and not invoked at all
/// when anything escaped the operation. It is the transactional on-commit hook (Django's
/// <c>on_commit</c>, Rails' <c>after_commit</c>) a handler needs when the work must not race
/// the rows the operation has not committed yet — a notice about the party this very operation
/// filed is read on a scope and a connection of its own, which cannot see them.
///
/// <para>Scoped, so it belongs to the OPERATION and not to a handler: a nested send resolves it
/// from the outermost send's scope (<see cref="AmbientUnitOfWork"/> joins both), so a composing
/// handler's registration and its inner handler's are drained together, once, by whoever owns
/// the transaction.</para>
///
/// <para>It is the OPERATION's commit that drains it, so a flow that opens no operation drains
/// nothing: a <c>Publish</c> (which opens no unit of its own — <c>PublishPipeline</c>) reaches
/// this through whatever send it was published from, and a top-level publish made outside any
/// send has no commit for a registration to wait on. Register from a handler.</para>
///
/// <para>This is not an escape hatch from the unit of work: there is still no ambient to
/// inspect and no way to suppress a commit, and what is registered here runs strictly after
/// one. Nor is it where a fact the operation does not own is written — that stays a scope of
/// its own, committed where it is written (messaging.md).</para></summary>
public sealed class AfterCommit
{
    readonly List<Func<Task>> _registered = [];
    readonly ILogger<AfterCommit>? _logger;

    // Optional: AddMessaging composes no logging, and a client host may compose none at all.
    // The drain below is the only thing here that has anything to say.
    public AfterCommit(ILogger<AfterCommit>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>Registers <paramref name="work"/> to run right after the operation commits.
    /// Everything it needs it carries as a value: it runs with the transaction already closed,
    /// so reaching back into the handler's <c>DbContext</c> from inside it reads a change
    /// tracker nobody will save again.</summary>
    public void Register(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        _registered.Add(work);
    }

    // No cancellation token, for the reason Rollback has none: the commit has happened, and
    // work registered against it must not be skippable by a cancellation that arrived too late.
    //
    // A failure is reported and swallowed rather than thrown. The answer the caller is waiting
    // for is about an operation that COMMITTED — raising here would tell the person at the
    // counter their save did not stand when it did, and the rollback this would reach is a
    // rollback of nothing. Per callback, so one bad registration does not lose the others.
    internal async Task Drain()
    {
        foreach (var work in _registered)
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "After-commit work failed; the operation stands committed.");
            }
        }

        _registered.Clear();
    }
}
