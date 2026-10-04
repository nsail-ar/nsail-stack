// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Sending;

namespace NSail.Messaging.Runtime.Pipelines;

// The generated endpoints' entry pipeline: every [Http] message's minimal API entry resolves one
// by [FromServices] and executes it, so this is the top-level send of an HTTP call the host
// itself serves. It names the in-process sender instead of asking which ISender the host
// registered, because an endpoint that reached a transport would be answering its own request.
public class InProcessSendPipeline<TMessage> : SendPipeline<TMessage>
    where TMessage : IMessage
{
    public InProcessSendPipeline(IServiceProvider services)
        : base(services, static scope => scope.GetRequiredService<InProcessSender<TMessage>>())
    {
    }
}

public class InProcessSendPipeline<TMessage, TResult> : SendPipeline<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    public InProcessSendPipeline(IServiceProvider services)
        : base(services, static scope => scope.GetRequiredService<InProcessSender<TMessage, TResult>>())
    {
    }
}
