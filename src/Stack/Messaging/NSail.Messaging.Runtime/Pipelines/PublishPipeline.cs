// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Runtime.Pipelines;

public class PublishPipeline<TMessage> : IPipeline
    where TMessage : IMessage
{
    readonly PipelineDelegate<TMessage> _next;

    public PublishPipeline(
        IEnumerable<IInterceptor<TMessage>> interceptors,
        IEnumerable<IPublisher<TMessage>> publishers)
    {
        var publisherArray = publishers.ToArray();

        _next = async (message, cancellationToken) =>
        {
            if (publisherArray.Length == 0)
                return;

            if (publisherArray.Length == 1)
            {
                await publisherArray[0].Publish(message, cancellationToken).ConfigureAwait(false);
                return;
            }

            var tasks = new Task[publisherArray.Length];
            for (int i = 0; i < publisherArray.Length; i++)
                tasks[i] = publisherArray[i].Publish(message, cancellationToken);

            await Task.WhenAll(tasks).ConfigureAwait(false);
        };

        foreach (var interceptor in interceptors.Reverse())
        {
            var currentNext = _next;
            _next = (message, cancellationToken) => interceptor.Invoke(message, currentNext, cancellationToken);
        }
    }

    // The ambient is entered here rather than contributed by whoever registers the gate, so it
    // cannot race an Add* that lands later — the same reason SendPipeline runs the message's
    // own DataAnnotations itself instead of registering an interceptor for them. The delivery's
    // MessageContext is opened in the same place and for the same reason: a subscriber reads
    // the headers this publish was called with, never the ones some earlier call left behind.
    public Task Execute(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        return AmbientPublish.Run(() =>
            AmbientMessageContext.Run(headers, () => _next((TMessage)message, cancellationToken)));
    }
}