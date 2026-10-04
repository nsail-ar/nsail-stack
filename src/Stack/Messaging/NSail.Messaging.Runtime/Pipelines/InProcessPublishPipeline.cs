// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Runtime.Pipelines;

public class InProcessPublishPipeline<TMessage> : PublishPipeline<TMessage>
    where TMessage : IMessage
{
    public InProcessPublishPipeline(
        IEnumerable<IInterceptor<TMessage>> interceptors,
        InProcessPublisher<TMessage> publisher)
        : base(interceptors, [publisher])
    {
    }
} 