// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Context;

// The MessageContext of the delivery this async flow is inside. It travels by AsyncLocal for
// the reason AmbientPublish and AmbientUnitOfWork do — the only channel a subscriber or a
// handler cannot forget to pass — and entering is internal to the runtime, so the pipelines
// are the only thing that can ever put a context there. No user code writes it: the write
// side of a header is the headers argument on the call it belongs to, which is what keeps
// the header and its operation in one expression instead of in a remembered call order.
static class AmbientMessageContext
{
    static readonly AsyncLocal<MessageContext?> Current = new();

    internal static MessageContext? Value
    {
        get { return Current.Value; }
    }

    // ASSIGNED, never merged with what was already there: an operation started inside another
    // delivery is its own operation, so an event published from a subscriber starts clean
    // instead of inheriting headers nobody put on it.
    //
    // The context is written, the work is STARTED, and the write is undone before this method
    // ever awaits: the flow that was started captured it, and the caller — which has not
    // awaited yet — must not keep it. Same order of operations as AmbientPublish.Run.
    internal static Task Run(IReadOnlyDictionary<string, string>? headers, Func<Task> start)
    {
        var previous = Current.Value;

        Current.Value = headers is null ? null : new MessageContext(headers);

        try
        {
            return start();
        }
        finally
        {
            Current.Value = previous;
        }
    }

    internal static Task<TResult> Run<TResult>(IReadOnlyDictionary<string, string>? headers, Func<Task<TResult>> start)
    {
        var previous = Current.Value;

        Current.Value = headers is null ? null : new MessageContext(headers);

        try
        {
            return start();
        }
        finally
        {
            Current.Value = previous;
        }
    }
}
