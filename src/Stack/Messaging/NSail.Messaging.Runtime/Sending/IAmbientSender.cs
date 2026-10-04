// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Sending;

/// <summary>A sender whose handler runs in this process, inside the operation's own service
/// scope and transaction. The pipeline in front of it is built from the scope Run hands back
/// rather than from the caller's, so an interceptor and a validator are constructed by — and
/// undone with — the same unit of work as the handler they guard. A transport sender does not
/// implement it: a send crossing a process boundary is a second operation by design.</summary>
public interface IAmbientSender
{
    Task Run(
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken);

    Task<TResult> Run<TResult>(
        Func<IServiceProvider, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
