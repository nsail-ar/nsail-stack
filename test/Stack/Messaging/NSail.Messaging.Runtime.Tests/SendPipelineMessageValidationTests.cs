// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Sending;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Messaging.Runtime.Tests;

// Pins the invariant behind the ValidationInterceptor removal: the message's own
// DataAnnotations must never be reachable ahead of the security gate, no matter which host
// composes it or when — an unauthorized caller meets Forbidden, never InvalidModel, and an
// authorized caller sending a malformed message still meets InvalidModel exactly as before.
public sealed class SendPipelineMessageValidationTests
{
    private static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);
        return services.BuildServiceProvider();
    }

    private sealed record CreateThing : IMessage
    {
        [Required]
        public string? Sku { get; set; }
    }

    private sealed record CountThings : IMessage<int>
    {
        [Required]
        public string? Sku { get; set; }
    }

    private sealed class CreateThingHandler : IHandler<CreateThing>
    {
        public int Count;

        public Task Handle(CreateThing message, CancellationToken ct)
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

    private sealed class Trace
    {
        public List<string> Steps { get; } = [];
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

    private sealed class AllowingInterceptor<TMessage> : IInterceptor<TMessage>
    {
        private readonly Trace _trace;

        public AllowingInterceptor(Trace trace)
        {
            _trace = trace;
        }

        public Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken ct)
        {
            _trace.Steps.Add("gate");
            return next(message, ct);
        }
    }

    private sealed class RecordingValidator<TMessage> : IValidator<TMessage>
    {
        private readonly Trace _trace;

        public RecordingValidator(Trace trace)
        {
            _trace = trace;
        }

        public Task<Problem?> Validate(TMessage message, CancellationToken ct)
        {
            _trace.Steps.Add("rule");
            return Task.FromResult<Problem?>(null);
        }
    }

    // The finding this test pins: an anonymous malformed send used to answer 400 InvalidModel
    // (naming Sku) before the gate ever ran, because ValidationInterceptor was a registered
    // IInterceptor that AddMessaging wired ahead of AddSecurityEnforcement. It is now a
    // structural call SendPipeline makes after every interceptor, so the gate answers first
    // regardless of registration order.
    [Fact]
    public async Task Unauthorized_and_malformed_answers_Forbidden_never_InvalidModel()
    {
        var trace = new Trace();
        var handler = new CreateThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IInterceptor<CreateThing>>(new DenyingInterceptor<CreateThing>(trace));
            s.AddScoped<ISender<CreateThing>, InProcessSender<CreateThing>>();
            s.AddSingleton<IHandler<CreateThing>>(handler);
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CreateThing { Sku = null }));

        Assert.Equal("Forbidden", exception.Code);
        Assert.Equal(403, exception.Status);
        Assert.Equal(["gate"], trace.Steps);
        Assert.Equal(0, handler.Count);
    }

    // Same invariant on the arity that returns a result.
    [Fact]
    public async Task Unauthorized_and_malformed_answers_Forbidden_never_InvalidModel_for_a_result_message()
    {
        var trace = new Trace();
        var handler = new CountThingsHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IInterceptor<CountThings, int>>(new DenyingResultInterceptor(trace));
            s.AddScoped<ISender<CountThings, int>, InProcessSender<CountThings, int>>();
            s.AddSingleton<IHandler<CountThings, int>>(handler);
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CountThings { Sku = null }));

        Assert.Equal("Forbidden", exception.Code);
        Assert.Equal(["gate"], trace.Steps);
        Assert.Equal(0, handler.Count);
    }

    private sealed class DenyingResultInterceptor : IInterceptor<CountThings, int>
    {
        private readonly Trace _trace;

        public DenyingResultInterceptor(Trace trace)
        {
            _trace = trace;
        }

        public Task<int> Invoke(CountThings message, PipelineDelegate<CountThings, int> next, CancellationToken ct)
        {
            _trace.Steps.Add("gate");
            throw new BusinessException(SecurityProblem.Forbidden(typeof(CountThings).Name));
        }
    }

    // The gate allows, the message is still malformed: today's behaviour must not regress.
    [Fact]
    public async Task Authorized_and_malformed_still_answers_InvalidModel_naming_the_field()
    {
        var trace = new Trace();
        var handler = new CreateThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IInterceptor<CreateThing>>(new AllowingInterceptor<CreateThing>(trace));
            s.AddScoped<ISender<CreateThing>, InProcessSender<CreateThing>>();
            s.AddSingleton<IHandler<CreateThing>>(handler);
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CreateThing { Sku = null }));

        Assert.Equal("InvalidModel", exception.Code);
        Assert.Equal(400, exception.Status);
        Assert.Equal("Sku", Assert.Single(exception.Issues).Source);
        Assert.Equal(["gate"], trace.Steps);
        Assert.Equal(0, handler.Count);
    }

    // Structural, not registration-dependent: no interceptor at all is registered here, and the
    // message is still refused before the handler runs.
    [Fact]
    public async Task A_malformed_message_is_refused_even_with_no_interceptors_registered()
    {
        var handler = new CreateThingHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<CreateThing>, InProcessSender<CreateThing>>();
            s.AddSingleton<IHandler<CreateThing>>(handler);
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CreateThing { Sku = null }));

        Assert.Equal("InvalidModel", exception.Code);
        Assert.Equal(0, handler.Count);
    }

    // DataAnnotations gate the business rules too: a rule should never inspect a message that
    // failed its own contract.
    [Fact]
    public async Task DataAnnotations_run_before_business_rule_validators()
    {
        var trace = new Trace();
        var handler = new CreateThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IValidator<CreateThing>>(new RecordingValidator<CreateThing>(trace));
            s.AddScoped<ISender<CreateThing>, InProcessSender<CreateThing>>();
            s.AddSingleton<IHandler<CreateThing>>(handler);
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CreateThing { Sku = null }));

        Assert.Equal("InvalidModel", exception.Code);
        Assert.Empty(trace.Steps);
        Assert.Equal(0, handler.Count);
    }

    // Non-vacuity: a well-formed message, authorized, still reaches the handler — the guard is
    // not a blanket refusal.
    [Fact]
    public async Task Authorized_and_well_formed_reaches_the_handler()
    {
        var trace = new Trace();
        var handler = new CreateThingHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IInterceptor<CreateThing>>(new AllowingInterceptor<CreateThing>(trace));
            s.AddScoped<ISender<CreateThing>, InProcessSender<CreateThing>>();
            s.AddSingleton<IHandler<CreateThing>>(handler);
        });

        await sp.GetRequiredService<Mediator>().Send(new CreateThing { Sku = "SKU-1" });

        Assert.Equal(1, handler.Count);
        Assert.Equal(["gate"], trace.Steps);
    }
}
