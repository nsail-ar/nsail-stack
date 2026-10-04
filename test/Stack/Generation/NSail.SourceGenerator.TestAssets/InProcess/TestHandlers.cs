// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.SourceGenerator.TestAssets.Http;

namespace NSail.SourceGenerator.TestAssets.InProcess;

// A message with no transport attribute: no sender is generated for it.
public class TestCommand : IMessage
{
    public string? Note { get; set; }
}

public class TestCommandHandler : IHandler<TestCommand>
{
    public Task Handle(TestCommand message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TestGetMessageHandler : IHandler<TestGetMessage, List<ExternalGetResult>>
{
    public Task<List<ExternalGetResult>> Handle(TestGetMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<ExternalGetResult>());
    }
}
