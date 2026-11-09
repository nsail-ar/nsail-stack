// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.ExceptionServices;
using NSail.Problems;

namespace NSail.Messaging.Runtime.Publishing;

public class InProcessPublisher<TMessage> : IPublisher<TMessage>
    where TMessage : IMessage
{
    readonly IHandler<TMessage>[] _handlers;
    readonly Subscriptions<TMessage> _subscriptions;

    public InProcessPublisher(
        IEnumerable<IHandler<TMessage>> handlers,
        Subscriptions<TMessage> subscriptions)
    {
        _handlers = handlers.ToArray();
        _subscriptions = subscriptions;
    }

    public async Task Publish(TMessage message, CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();

        for (int i = 0; i < _handlers.Length; i++)
        {
            var handler = _handlers[i];
            await Run(() => handler.Handle(message, cancellationToken), failures).ConfigureAwait(false);
        }

        var actions = _subscriptions.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            await Run(() => action(message, cancellationToken), failures).ConfigureAwait(false);
        }

        if (failures.Count == 0)
        {
            return;
        }

        // The callers that tell a cancellation or a refusal from a bug do it by exception type
        // and cannot see inside an aggregate: a publish that only left or only refused ends as
        // the first of them, and the aggregate is for the mix that holds a bug.
        if (failures.Count == 1 || failures.All(Answers))
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException(failures);
    }

    static bool Answers(Exception exception)
    {
        return exception is OperationCanceledException or BusinessException;
    }

    static async Task Run(Func<Task> start, List<Exception> failures)
    {
        try
        {
            await start().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
    }
}
