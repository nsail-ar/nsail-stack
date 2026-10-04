// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Pipelines;

public delegate Task<TResult> PipelineDelegate<TMessage, TResult>(TMessage message, CancellationToken cancellationToken);

public delegate Task PipelineDelegate<TMessage>(TMessage message, CancellationToken cancellationToken);

