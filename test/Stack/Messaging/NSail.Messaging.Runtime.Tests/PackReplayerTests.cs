// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Packs;
using NSail.Metadata;

namespace NSail.Messaging.Runtime.Tests;

public sealed class PackReplayerTests
{
    public sealed class Step : IMessage
    {
        public int N { get; set; }
    }

    sealed class ScriptedMediator : Mediator
    {
        readonly Func<Step, Task> _send;

        public ScriptedMediator(Func<Step, Task> send)
            : base(new ServiceCollection().BuildServiceProvider())
        {
            _send = send;
        }

        public override Task Send(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
        {
            return _send((Step)message);
        }
    }

    static (PackReplayer Replayer, Pack Pack) Build(Func<Step, Task> send, int steps)
    {
        var registry = new MessageRegistry(new MetadataProvider(), [typeof(Step)]);
        var key = registry.KeyFor(typeof(Step));
        var pack = new Pack
        {
            Name = "p",
            Steps = Enumerable.Range(0, steps)
                .Select(n => new PackStep { Message = key, Body = JsonSerializer.SerializeToElement(new { N = n }) })
                .ToList(),
        };

        return (new PackReplayer(new ScriptedMediator(send), registry), pack);
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_replay_instead_of_recording_step_errors()
    {
        using var cts = new CancellationTokenSource();
        var (replayer, pack) = Build(step =>
        {
            if (step.N == 1)
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        }, 4);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replayer.Replay(pack, PackFailure.Collect, cts.Token));
    }

    [Fact]
    public async Task Collect_still_gathers_real_step_failures()
    {
        var (replayer, pack) = Build(step =>
        {
            return step.N % 2 == 1 ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        }, 4);

        var replay = await replayer.Replay(pack, PackFailure.Collect);

        Assert.Equal(2, replay.Imported);
        Assert.Equal(2, replay.Errors.Count);
    }
}
