// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Pipelines;

public interface IPipeline
{
    Task Execute(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken);
}

public interface IPipeline<TResult>
{
    Task<TResult> Execute(IMessage<TResult> message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken);
}
