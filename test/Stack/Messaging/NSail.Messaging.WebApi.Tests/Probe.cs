// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime.Context;
using NSail.SourceGeneration.Annotations;

namespace NSail.Messaging.WebApi.Tests;

// One hop's worth of message, and nothing else: the far side answers with what its accessor
// read, so a test can ask the question the whole mechanism exists to answer — did the header
// the caller named survive the wire, under the name it was sent with?
[Http(Method.Get, "api/probe/delivery")]
public sealed class ReadDelivery : IMessage<string>
{
    public const string None = "<none>";
}

public sealed class DeliveryProbeHandler : IHandler<ReadDelivery, string>
{
    readonly MessageContextAccessor _deliveries;

    public DeliveryProbeHandler(MessageContextAccessor deliveries)
    {
        _deliveries = deliveries;
    }

    public Task<string> Handle(ReadDelivery message, CancellationToken cancellationToken)
    {
        if (_deliveries.Context is not { } context)
        {
            return Task.FromResult(ReadDelivery.None);
        }

        var headers = context.Headers
            .OrderBy(header => header.Key, StringComparer.Ordinal)
            .Select(header => $"{header.Key}={header.Value}");

        return Task.FromResult(string.Join(";", headers));
    }
}

// The real generated code on both ends of the hop: the endpoints the host maps and the senders
// the caller sends through are the ones every kit ships, emitted from the same templates.
public static partial class Wire
{
    [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
    public static partial void AddProbeEndpoints(this IServiceCollection services);

    [Generated(NSail.SourceGeneration.Annotations.Http.Clients)]
    public static partial void AddProbeClients(this IServiceCollection services);
}
