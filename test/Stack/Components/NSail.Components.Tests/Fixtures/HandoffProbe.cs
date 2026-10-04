// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime;

namespace NSail.Components.Tests.Fixtures;

public sealed class ProbeRead : IMessage<ProbeAnswer>
{
    public Guid SubjectId { get; set; }
}

public sealed class ProbeAnswer
{
    public int Count { get; set; }
}

/// <summary>A <see cref="Mediator"/> that answers every read with the same figure and counts
/// how many times it was asked — the count IS the property under test.</summary>
public sealed class CountingMediator : Mediator
{
    readonly int _count;

    public CountingMediator(int count)
        : base(new EmptyServices())
    {
        _count = count;
    }

    public int Sends { get; private set; }

    public override async Task<TResult> Send<TResult>(IMessage<TResult> message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        Sends++;

        // Yields deliberately: a read is a round trip, so it costs the component a second
        // render — which is the render the handoff exists to save, and what these tests count.
        await Task.Yield();

        return (TResult)(object)new ProbeAnswer { Count = _count };
    }

    sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
