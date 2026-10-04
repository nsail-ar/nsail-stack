// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Sending;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

public sealed class SendPipelineValidatorTests
{
    private static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);
        return services.BuildServiceProvider();
    }

    private sealed record SaveThing : IMessage;

    private sealed record CountThings : IMessage<int>;

    private sealed class SaveThingHandler : IHandler<SaveThing>
    {
        public int Count;

        public Task Handle(SaveThing message, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountThingsHandler : IHandler<CountThings, int>
    {
        public int Count;

        public Task<int> Handle(CountThings message, CancellationToken ct)
        {
            Count++;
            return Task.FromResult(7);
        }
    }

    // The trace every ordering assertion below reads: one list, appended to by whoever ran.
    private sealed class Trace
    {
        public List<string> Steps { get; } = [];
    }

    private sealed class RecordingValidator<TMessage> : IValidator<TMessage>
    {
        private readonly Trace _trace;
        private readonly string _name;
        private readonly Problem? _refusal;

        public RecordingValidator(Trace trace, string name, Problem? refusal)
        {
            _trace = trace;
            _name = name;
            _refusal = refusal;
        }

        public Task<Problem?> Validate(TMessage message, CancellationToken ct)
        {
            _trace.Steps.Add(_name);
            return Task.FromResult(_refusal);
        }
    }

    private sealed class RecordingInterceptor<TMessage> : IInterceptor<TMessage>
    {
        private readonly Trace _trace;

        public RecordingInterceptor(Trace trace)
        {
            _trace = trace;
        }

        public Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken ct)
        {
            _trace.Steps.Add("interceptor");
            return next(message, ct);
        }
    }

    // Stands in for the security gate: refuses before anything downstream can answer.
    private sealed class DenyingInterceptor<TMessage> : IInterceptor<TMessage>
    {
        private readonly Trace _trace;

        public DenyingInterceptor(Trace trace)
        {
            _trace = trace;
        }

        public Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken ct)
        {
            _trace.Steps.Add("gate");
            throw new BusinessException(SecurityProblem.Forbidden(typeof(TMessage).Name));
        }
    }

    private static Problem Refusal()
    {
        return BusinessProblem.RuleViolation("Thing", "the thing is not saveable");
    }

    [Fact]
    public async Task Refusal_becomes_a_business_exception_and_the_handler_never_runs()
    {
        var trace = new Trace();
        var handler = new SaveThingHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<SaveThing>, InProcessSender<SaveThing>>();
            s.AddSingleton<IHandler<SaveThing>>(handler);
            s.AddSingleton<IValidator<SaveThing>>(new RecordingValidator<SaveThing>(trace, "rule", Refusal()));
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new SaveThing()));

        Assert.Equal("RuleViolation", exception.Code);
        Assert.Equal(422, exception.Status);
        Assert.Equal("the thing is not saveable", Assert.Single(exception.Issues).Message);
        Assert.Equal(0, handler.Count);
        Assert.Equal(["rule"], trace.Steps);
    }

    [Fact]
    public async Task A_validator_that_returns_no_problem_lets_the_send_through()
    {
        var trace = new Trace();
        var handler = new SaveThingHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<SaveThing>, InProcessSender<SaveThing>>();
            s.AddSingleton<IHandler<SaveThing>>(handler);
            s.AddSingleton<IValidator<SaveThing>>(new RecordingValidator<SaveThing>(trace, "rule", null));
        });

        await sp.GetRequiredService<Mediator>().Send(new SaveThing());

        Assert.Equal(1, handler.Count);
        Assert.Equal(["rule"], trace.Steps);
    }

    // The reason the seam exists rather than an app registering a plain closed-generic
    // interceptor: with interceptors the chain follows registration order, so a rule composed
    // by the app's own wiring can land ahead of a gate the host turns on later. Registered
    // first here and still asked last.
    [Fact]
    public async Task A_validator_runs_after_every_interceptor_however_they_were_registered()
    {
        var trace = new Trace();
        var handler = new SaveThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IValidator<SaveThing>>(new RecordingValidator<SaveThing>(trace, "rule", null));
            s.AddSingleton<IInterceptor<SaveThing>>(new RecordingInterceptor<SaveThing>(trace));
            s.AddScoped<ISender<SaveThing>, InProcessSender<SaveThing>>();
            s.AddSingleton<IHandler<SaveThing>>(handler);
        });

        await sp.GetRequiredService<Mediator>().Send(new SaveThing());

        Assert.Equal(["interceptor", "rule"], trace.Steps);
        Assert.Equal(1, handler.Count);
    }

    // Forbidden before Invalid: a caller the gate turns away must not be able to tell a
    // rule-breaking payload from a valid one, and must not make a rule spend a query.
    [Fact]
    public async Task A_refusing_gate_answers_before_a_validator_is_asked()
    {
        var trace = new Trace();
        var handler = new SaveThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IValidator<SaveThing>>(new RecordingValidator<SaveThing>(trace, "rule", Refusal()));
            s.AddSingleton<IInterceptor<SaveThing>>(new DenyingInterceptor<SaveThing>(trace));
            s.AddScoped<ISender<SaveThing>, InProcessSender<SaveThing>>();
            s.AddSingleton<IHandler<SaveThing>>(handler);
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new SaveThing()));

        Assert.Equal("Forbidden", exception.Code);
        Assert.Equal(["gate"], trace.Steps);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task The_first_refusal_wins_and_the_next_validator_is_not_asked()
    {
        var trace = new Trace();
        var handler = new SaveThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IValidator<SaveThing>>(new RecordingValidator<SaveThing>(trace, "first", Refusal()));
            s.AddSingleton<IValidator<SaveThing>>(new RecordingValidator<SaveThing>(trace, "second", null));
            s.AddScoped<ISender<SaveThing>, InProcessSender<SaveThing>>();
            s.AddSingleton<IHandler<SaveThing>>(handler);
        });

        await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new SaveThing()));

        Assert.Equal(["first"], trace.Steps);
        Assert.Equal(0, handler.Count);
    }

    // One IValidator<TMessage> whether or not the message carries a result: a save-time rule
    // has nothing to say about what comes back, so the app never picks an arity.
    [Fact]
    public async Task A_message_that_returns_a_result_is_refused_by_the_same_contract()
    {
        var trace = new Trace();
        var handler = new CountThingsHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<CountThings, int>, InProcessSender<CountThings, int>>();
            s.AddSingleton<IHandler<CountThings, int>>(handler);
            s.AddSingleton<IValidator<CountThings>>(new RecordingValidator<CountThings>(trace, "rule", Refusal()));
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CountThings()));

        Assert.Equal("RuleViolation", exception.Code);
        Assert.Equal(0, handler.Count);
        Assert.Equal(["rule"], trace.Steps);
    }

    [Fact]
    public async Task A_message_that_returns_a_result_reaches_its_handler_when_no_rule_refuses()
    {
        var trace = new Trace();
        var handler = new CountThingsHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<CountThings, int>, InProcessSender<CountThings, int>>();
            s.AddSingleton<IHandler<CountThings, int>>(handler);
            s.AddSingleton<IValidator<CountThings>>(new RecordingValidator<CountThings>(trace, "rule", null));
        });

        var result = await sp.GetRequiredService<Mediator>().Send(new CountThings());

        Assert.Equal(7, result);
        Assert.Equal(1, handler.Count);
        Assert.Equal(["rule"], trace.Steps);
    }

    [Fact]
    public async Task A_message_with_no_validators_reaches_its_handler()
    {
        var handler = new SaveThingHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<SaveThing>, InProcessSender<SaveThing>>();
            s.AddSingleton<IHandler<SaveThing>>(handler);
        });

        await sp.GetRequiredService<Mediator>().Send(new SaveThing());

        Assert.Equal(1, handler.Count);
    }
}
