// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGeneration.Annotations;

public enum Services
{
    Registration = 10
}

public enum Http
{
    /// <summary>Minimal API endpoints for the [Http] messages handled in scope (receive side).</summary>
    Endpoints = 21,

    /// <summary>HttpSender per [Http] message, registered as its ISender (remote send side).</summary>
    Clients = 22,

    /// <summary>InProcessSender per [Http] message, registered as its ISender (local counterpart of Clients, for the host that owns the handlers).</summary>
    InProcess = 23,
}

public enum SignalR
{
    /// <summary>A PushPublisher per [Pushed] message in scope, registered as one of its IPublishers (server side: a publish also reaches the tenant's open clients).</summary>
    Hubs = 31,

    /// <summary>A PushedMessage per [Pushed] message in scope, the closed list a client's HubFeed publishes from (client side; counterpart of Hubs, as Http.Clients is of Http.Endpoints).</summary>
    Clients = 32,
}

public enum MassTransit
{
    Consumers = 41,
    Producers = 42
}

public enum Policies
{
    /// <summary>A PolicyHandler per message with [PolicyField]-marked fields, plus their DI factories.</summary>
    Handlers = 51
}

/// <summary>
/// Marks a partial method whose body is emitted by the source generator
/// for the given target. Scan scope: the current compilation, or the
/// assemblies named by [Source].
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class GeneratedAttribute : Attribute
{
    public GeneratedAttribute(Services target)
    {
    }

    public GeneratedAttribute(Http artifact)
    {
    }

    public GeneratedAttribute(SignalR artifact)
    {
    }

    public GeneratedAttribute(MassTransit artifact)
    {
    }

    public GeneratedAttribute(Policies artifact)
    {
    }
}
