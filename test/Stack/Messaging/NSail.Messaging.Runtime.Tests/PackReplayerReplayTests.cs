// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Packs;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Messaging.Runtime.Tests;

public sealed class PackReplayerReplayTests
{
    public sealed record Step : IMessage
    {
        public string Name { get; init; } = "";
    }

    sealed class RecordingMediator : Mediator
    {
        readonly Func<Step, Task> _send;

        public RecordingMediator(Func<Step, Task> send)
            : base(new ServiceCollection().BuildServiceProvider())
        {
            _send = send;
        }

        public List<string> Sent { get; } = [];

        public override Task Send(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
        {
            var step = (Step)message;

            Sent.Add(step.Name);

            return _send(step);
        }
    }

    static readonly MessageRegistry Registry = new(new MetadataProvider(), [typeof(Step)]);

    static PackStep StepOf(string name)
    {
        return new PackStep
        {
            Message = Registry.KeyFor(typeof(Step)),
            Body = JsonSerializer.SerializeToElement(new { name }),
        };
    }

    static Pack PackOf(params PackStep[] steps)
    {
        return new Pack { Name = "pack", Steps = [.. steps] };
    }

    static BusinessException Refusal(string code)
    {
        return new BusinessException(code, code, [new Issue(code, $"{code} says no", "step")]);
    }

    [Fact]
    public async Task A_step_whose_subject_already_exists_is_skipped_and_the_rest_still_run()
    {
        var mediator = new RecordingMediator(step =>
        {
            return step.Name == "b" ? throw Refusal("AlreadyExists") : Task.CompletedTask;
        });

        var replay = await new PackReplayer(mediator, Registry).Replay(PackOf(StepOf("a"), StepOf("b"), StepOf("c")), PackFailure.Stop);

        Assert.Equal(2, replay.Imported);
        Assert.Equal(1, replay.Skipped);
        Assert.Empty(replay.Errors);
        Assert.Equal(["a", "b", "c"], mediator.Sent);
    }

    [Fact]
    public async Task Stop_ends_the_replay_at_the_first_failed_step()
    {
        var mediator = new RecordingMediator(step =>
        {
            return step.Name == "b" ? throw Refusal("Invalid") : Task.CompletedTask;
        });

        var replay = await new PackReplayer(mediator, Registry).Replay(PackOf(StepOf("a"), StepOf("b"), StepOf("c")), PackFailure.Stop);

        Assert.Equal(1, replay.Imported);
        var error = Assert.Single(replay.Errors);
        Assert.Equal(1, error.Index);
        Assert.Equal("Invalid says no", error.Detail);
        Assert.Equal(["a", "b"], mediator.Sent);
    }

    [Fact]
    public async Task Collect_attempts_every_step_and_keeps_each_error()
    {
        var mediator = new RecordingMediator(step =>
        {
            return step.Name != "b" ? throw new InvalidOperationException($"{step.Name} broke") : Task.CompletedTask;
        });

        var replay = await new PackReplayer(mediator, Registry).Replay(PackOf(StepOf("a"), StepOf("b"), StepOf("c")), PackFailure.Collect);

        Assert.Equal(1, replay.Imported);
        Assert.Equal([0, 2], replay.Errors.Select(error => error.Index));
        Assert.Equal(["a broke", "c broke"], replay.Errors.Select(error => error.Detail));
        Assert.Equal(["a", "b", "c"], mediator.Sent);
    }

    [Fact]
    public async Task A_step_the_registry_does_not_know_is_a_stale_pack_error_and_is_never_sent()
    {
        var mediator = new RecordingMediator(_ => Task.CompletedTask);
        var stale = new PackStep { Message = "Gone.Feature.Message", Body = JsonSerializer.SerializeToElement(new { }) };

        var replay = await new PackReplayer(mediator, Registry).Replay(PackOf(stale, StepOf("a")), PackFailure.Collect);

        var error = Assert.Single(replay.Errors);
        Assert.Contains("stale", error.Detail, StringComparison.Ordinal);
        Assert.Contains("Gone.Feature.Message", error.Detail, StringComparison.Ordinal);
        Assert.Equal(["a"], mediator.Sent);
    }
}
