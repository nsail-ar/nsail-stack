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

    // A rule composed at run time, read by an attribute through ValidationContext.GetService
    // from the scope of the operation that carries the message.
    private sealed class DigitRule
    {
        public int Digits { get; init; } = 7;

        public int Asked { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class DigitsByRuleAttribute : ValidationAttribute, ICodedValidation
    {
        public string Code => "DigitsByRule";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string text || validationContext.GetService(typeof(DigitRule)) is not DigitRule rule)
            {
                return ValidationResult.Success;
            }

            rule.Asked++;

            return text.Length == rule.Digits ? ValidationResult.Success : new ValidationResult("Wrong digits");
        }
    }

    private sealed record Holder
    {
        [DigitsByRule]
        public string? Number { get; set; }
    }

    private sealed record CreateDocument : IMessage
    {
        [DigitsByRule]
        public string? Number { get; set; }

        [Validated]
        public Holder? Holder { get; set; }
    }

    private sealed record CountDocuments : IMessage<int>
    {
        [DigitsByRule]
        public string? Number { get; set; }
    }

    private sealed record CheckDocument : IMessage, IValidatableObject
    {
        public string? Number { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (validationContext.GetService(typeof(DigitRule)) is DigitRule rule && Number?.Length != rule.Digits)
            {
                yield return new ValidationResult("Wrong digits", [nameof(Number)]);
            }
        }
    }

    private sealed class CreateDocumentHandler : IHandler<CreateDocument>
    {
        private readonly DigitRule _rule;
        private readonly Trace _trace;

        public CreateDocumentHandler(DigitRule rule, Trace trace)
        {
            _rule = rule;
            _trace = trace;
        }

        public Task Handle(CreateDocument message, CancellationToken ct)
        {
            _trace.Steps.Add($"handled after {_rule.Asked} asks");
            return Task.CompletedTask;
        }
    }

    private sealed class CountDocumentsHandler : IHandler<CountDocuments, int>
    {
        public Task<int> Handle(CountDocuments message, CancellationToken ct)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class CheckDocumentHandler : IHandler<CheckDocument>
    {
        public Task Handle(CheckDocument message, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildDocuments(bool rule)
    {
        return Build(s =>
        {
            if (rule)
            {
                s.AddScoped<DigitRule>();
            }

            s.AddSingleton<Trace>();
            s.AddScoped<ISender<CreateDocument>, InProcessSender<CreateDocument>>();
            s.AddScoped<IHandler<CreateDocument>, CreateDocumentHandler>();
            s.AddScoped<ISender<CountDocuments, int>, InProcessSender<CountDocuments, int>>();
            s.AddScoped<IHandler<CountDocuments, int>, CountDocumentsHandler>();
            s.AddScoped<ISender<CheckDocument>, InProcessSender<CheckDocument>>();
            s.AddScoped<IHandler<CheckDocument>, CheckDocumentHandler>();
        });
    }

    [Fact]
    public async Task An_attribute_on_the_message_refuses_by_a_service_it_reads()
    {
        var sp = BuildDocuments(rule: true);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CreateDocument { Number = "123" }));

        Assert.Equal("InvalidModel", exception.Code);

        var issue = Assert.Single(exception.Issues);

        Assert.Equal("DigitsByRule", issue.Code);
        Assert.Equal("Number", issue.Source);
        Assert.Empty(sp.GetRequiredService<Trace>().Steps);
    }

    [Fact]
    public async Task An_attribute_on_a_result_message_refuses_by_a_service_it_reads()
    {
        var sp = BuildDocuments(rule: true);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CountDocuments { Number = "123" }));

        var issue = Assert.Single(exception.Issues);

        Assert.Equal("DigitsByRule", issue.Code);
        Assert.Equal("Number", issue.Source);
    }

    [Fact]
    public async Task An_attribute_on_a_validated_model_refuses_by_a_service_it_reads()
    {
        var sp = BuildDocuments(rule: true);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CreateDocument { Number = "1234567", Holder = new Holder { Number = "123" } }));

        var issue = Assert.Single(exception.Issues);

        Assert.Equal("DigitsByRule", issue.Code);
        Assert.Equal("Holder.Number", issue.Source);
    }

    [Fact]
    public async Task A_validatable_message_refuses_by_a_service_it_reads()
    {
        var sp = BuildDocuments(rule: true);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            sp.GetRequiredService<Mediator>().Send(new CheckDocument { Number = "123" }));

        Assert.Equal("Number", Assert.Single(exception.Issues).Source);
    }

    // The attribute reads the rule from the operation's own scope: the handler resolved from
    // that scope sees the asks the validation made on the same instance.
    [Fact]
    public async Task The_service_is_the_one_in_the_operations_scope()
    {
        var sp = BuildDocuments(rule: true);

        await sp.GetRequiredService<Mediator>().Send(
            new CreateDocument { Number = "1234567", Holder = new Holder { Number = "7654321" } });

        Assert.Equal(["handled after 2 asks"], sp.GetRequiredService<Trace>().Steps);
    }

    // With nothing registered for what it asks, the attribute decides — here it passes — and
    // the validator never throws on its account.
    [Fact]
    public async Task With_no_service_registered_the_attribute_decides()
    {
        var sp = Build(s =>
        {
            s.AddSingleton<Trace>();
            s.AddScoped<ISender<CheckDocument>, InProcessSender<CheckDocument>>();
            s.AddScoped<IHandler<CheckDocument>, CheckDocumentHandler>();
            s.AddScoped<ISender<CountDocuments, int>, InProcessSender<CountDocuments, int>>();
            s.AddScoped<IHandler<CountDocuments, int>, CountDocumentsHandler>();
        });

        await sp.GetRequiredService<Mediator>().Send(new CheckDocument { Number = "123" });

        Assert.Equal(1, await sp.GetRequiredService<Mediator>().Send(new CountDocuments { Number = "123" }));
    }

    [Fact]
    public void Validated_with_no_provider_the_context_answers_no_service()
    {
        Assert.Null(MessageValidator.Validate(new CreateDocument { Number = "123" }));
    }
}
