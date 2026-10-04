// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Metadata;

namespace NSail.Telemetry;

/// <summary>The span: registered as an open-generic Mediator interceptor, so every send and
/// every publish is one span named after its message and every nested send is that span's
/// child. Registered by AddTelemetry, which runs before any kit's AddSecurityEnforcement,
/// so it wraps the gate too and a refusal is a recorded outcome rather than a hole.
/// It does not wrap the unit of work: interceptors are built from the scope IAmbientSender
/// opens, so the transaction's COMMIT closes after the span does.</summary>
public sealed class TelemetryInterceptor<TMessage> : IInterceptor<TMessage>
    where TMessage : IMessage
{
    readonly MetadataProvider _metadata;

    public TelemetryInterceptor(MetadataProvider metadata)
    {
        _metadata = metadata;
    }

    public async Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        using var activity = MessageSpan.Start(_metadata, typeof(TMessage));

        try
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            MessageSpan.Fail(activity, exception);
            throw;
        }

        MessageSpan.Succeed(activity);
    }
}

public sealed class TelemetryInterceptor<TMessage, TResult> : IInterceptor<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    readonly MetadataProvider _metadata;

    public TelemetryInterceptor(MetadataProvider metadata)
    {
        _metadata = metadata;
    }

    public async Task<TResult> Invoke(TMessage message, PipelineDelegate<TMessage, TResult> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        using var activity = MessageSpan.Start(_metadata, typeof(TMessage));
        TResult result;

        try
        {
            result = await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            MessageSpan.Fail(activity, exception);
            throw;
        }

        MessageSpan.Succeed(activity);

        return result;
    }
}
