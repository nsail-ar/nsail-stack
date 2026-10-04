// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.UnitOfWork;

/// <summary>A store that can carry one in-process send — the outermost one and every send
/// nested inside it — as a single transaction. The runtime resolves every registered
/// implementation from the send's own scope, opens them before the handler runs, commits them
/// when the outermost handler returns and rolls them back when anything escapes it. A host
/// with no store registered (a client, a Wasm host) simply has nothing to enlist.</summary>
public interface IUnitOfWork
{
    Task Begin(CancellationToken cancellationToken);

    Task Commit(CancellationToken cancellationToken);

    /// <summary>Takes no token on purpose: undoing is what has to happen when the operation was
    /// already cancelled, so cancellation must not be able to skip it.</summary>
    Task Rollback();
}
