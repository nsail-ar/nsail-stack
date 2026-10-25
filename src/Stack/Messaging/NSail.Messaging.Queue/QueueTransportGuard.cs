// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Hosting;

namespace NSail.Messaging.Queue;

/// <summary>What the install may say about <c>Queue:Transport</c>, asked once at startup in
/// the shape every other incompatible configuration is refused in (<c>NSail.Data</c>'s
/// <c>Tenancy:RequireRowSecurity</c>): the broker names are declared so an install states its
/// intent in one place, and refused by name until the brokers' own story lands — the
/// alternative is an install that reads as configured for RabbitMQ and quietly queues
/// in-process instead.</summary>
sealed class QueueTransportGuard : IHostedService
{
    /// <summary>The in-process transport, which is also what an install that states nothing
    /// gets: the durable row IS the queue and the drain is a job of this process, so a default
    /// install provisions nothing it does not provision today.</summary>
    public const string InProcess = "InProcess";

    static readonly string[] Brokers = ["Postgres", "RabbitMq", "AmazonSqs", "AzureServiceBus"];

    readonly QueueOptions _options;

    public QueueTransportGuard(QueueOptions options)
    {
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Check(_options);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static void Check(QueueOptions options)
    {
        if (options.Transport is not { Length: > 0 } transport
            || string.Equals(transport, InProcess, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Brokers.Any(broker => string.Equals(broker, transport, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Queue:Transport is {transport}, and no broker is carried yet: the only transport this install can serve is {InProcess}, where the durable row is the queue and the drain is a job of this process. Clear the setting, or name {InProcess}.");
        }

        throw new InvalidOperationException(
            $"Queue:Transport is {transport}, which is not a transport this install knows. The ones it names are {InProcess} and, once the brokers' own story lands, {string.Join(", ", Brokers)}.");
    }
}
