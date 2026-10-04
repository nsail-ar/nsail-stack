// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Problems;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A <see cref="Mediator"/> that answers every send with the Problem it was given, so
/// a test can put a server's refusal in front of a component without standing up a pipeline,
/// a transport or a handler.</summary>
public sealed class RefusingMediator : Mediator
{
    public RefusingMediator()
        : base(new EmptyServices())
    {
    }

    public Problem? Refusal { get; set; }

    public int Sends { get; private set; }

    public override Task Send(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        Sends++;

        if (Refusal is not null)
        {
            throw new BusinessException(Refusal);
        }

        return Task.CompletedTask;
    }

    sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
