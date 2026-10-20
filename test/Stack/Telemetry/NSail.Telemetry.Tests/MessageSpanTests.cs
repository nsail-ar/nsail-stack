// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Consulting.Patients;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Metadata;
using NSail.Problems;
using NSail.Security;

namespace NSail.Telemetry.Tests;

// One span per Mediator operation, named for the message, nested the way the sends are.
public sealed class MessageSpanTests
{
    [Fact]
    public async Task A_send_is_one_span_named_by_area_feature_and_message()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<CreatePatient, Guid>>(new CreatePatientHandler());
        });

        await using var scope = app.Services.CreateAsyncScope();

        await Harness.Mediator(scope.ServiceProvider).Send(new CreatePatient("Juan Perez", "20123456789", "no notes"));

        var span = Assert.Single(recorder.Stopped);

        Assert.Equal("Consulting.Patients.CreatePatient", span.DisplayName);
        Assert.Equal(MessageSpan.Succeeded, span.GetTagItem(MessageSpan.OutcomeTag));
    }

    [Fact]
    public async Task A_nested_send_is_the_outer_sends_child()
    {
        using var recorder = new SpanRecorder();

        var outer = new CreatePatientHandler();

        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<CreatePatient, Guid>>(outer);
            services.AddSingleton<IHandler<ArchivePatient>>(new ArchivePatientHandler());
        });

        await using var scope = app.Services.CreateAsyncScope();

        var mediator = Harness.Mediator(scope.ServiceProvider);

        outer.Nested = () => mediator.Send(new ArchivePatient(Guid.NewGuid()));

        await mediator.Send(new CreatePatient("Juan Perez", "20123456789", string.Empty));

        var spans = recorder.Stopped;

        Assert.Equal(2, spans.Count);

        var inner = spans.Single(span => span.DisplayName == "Consulting.Patients.ArchivePatient");
        var parent = spans.Single(span => span.DisplayName == "Consulting.Patients.CreatePatient");

        Assert.Equal(parent.SpanId, inner.ParentSpanId);
        Assert.Equal(parent.TraceId, inner.TraceId);
    }

    [Fact]
    public async Task A_refusal_is_a_recorded_outcome_and_a_code()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false);

        await using var scope = app.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<BusinessException>(() =>
            Harness.Mediator(scope.ServiceProvider).Send(new RefusedPatient()));

        var span = Assert.Single(recorder.Stopped);

        Assert.Equal(MessageSpan.Refused, span.GetTagItem(MessageSpan.OutcomeTag));
        Assert.Equal(RefusedPatientHandler.Code, span.GetTagItem(MessageSpan.ProblemTag));
    }

    // Every rule of the house refuses under the one Problem code "RuleViolation" and names
    // itself on the issue, so the code alone cannot tell two rules apart: what a reader needs
    // is the key the rule's own sentence lives at.
    [Fact]
    public async Task A_rule_names_itself_as_the_key_its_sentence_lives_at()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false);

        await using var scope = app.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<BusinessException>(() =>
            Harness.Mediator(scope.ServiceProvider).Send(new RuledPatient()));

        var span = Assert.Single(recorder.Stopped);

        Assert.Equal(MessageSpan.Refused, span.GetTagItem(MessageSpan.OutcomeTag));
        Assert.Equal("RuleViolation", span.GetTagItem(MessageSpan.ProblemTag));
        Assert.Equal($"RuleViolation.{RuledPatientHandler.Rule}", span.GetTagItem(MessageSpan.RuleTag));

        // The issue's message is the one part of a refusal free to quote the value that caused
        // it, so no tag carries it.
        Assert.DoesNotContain("Juan Perez", span.TagObjects.Select(tag => tag.Value?.ToString() ?? string.Empty));
    }

    [Fact]
    public async Task A_crash_is_the_exceptions_type_and_no_code()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false);

        await using var scope = app.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Harness.Mediator(scope.ServiceProvider).Send(new CrashedPatient()));

        var span = Assert.Single(recorder.Stopped);

        Assert.Equal(MessageSpan.Failed, span.GetTagItem(MessageSpan.OutcomeTag));
        Assert.Equal(typeof(InvalidOperationException).FullName, span.GetTagItem(MessageSpan.FailureTag));
        Assert.Null(span.GetTagItem(MessageSpan.ProblemTag));
        Assert.Null(span.GetTagItem(MessageSpan.RuleTag));
    }

    // The tag the failure log is searched by: without it, "why did MY last action fail" has
    // no answer that is only the asker's.
    [Fact]
    public async Task A_send_names_the_acting_user()
    {
        var user = Guid.NewGuid();

        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<CreatePatient, Guid>>(new CreatePatientHandler());
        });

        await using var scope = app.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<SessionProvider>().Session = new Session
        {
            IsAuthenticated = true,
            UserId = user,
        };

        await Harness.Mediator(scope.ServiceProvider).Send(new CreatePatient("Juan Perez", "20123456789", "no notes"));

        Assert.Equal(user, Assert.Single(recorder.Stopped).GetTagItem(MessageSpan.UserTag));
    }

    // Absent and not empty: a search for one person's own operations must not match the ones
    // that were nobody's.
    [Fact]
    public async Task An_anonymous_send_names_nobody()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<CreatePatient, Guid>>(new CreatePatientHandler());
        });

        await using var scope = app.Services.CreateAsyncScope();

        await Harness.Mediator(scope.ServiceProvider).Send(new CreatePatient("Juan Perez", "20123456789", "no notes"));

        Assert.Null(Assert.Single(recorder.Stopped).GetTagItem(MessageSpan.UserTag));
    }

    // Whoever reads a failure back resolves the span's name through the message registry, which
    // is also what a policy grants, so the two renderings have to stay one. They are written
    // twice because MessageSpan holds a MetadataProvider and not a registry — a registry is the
    // whole composition — and this is what keeps them from drifting.
    [Fact]
    public void The_span_name_is_what_the_registry_calls_the_message()
    {
        Type[] messages = [typeof(CreatePatient), typeof(ArchivePatient), typeof(RefusedPatient)];

        var metadata = new MetadataProvider();
        var registry = new MessageRegistry(metadata, messages);

        foreach (var message in messages)
        {
            Assert.Equal(registry.KeyFor(message), MessageSpan.NameFor(metadata, message));
        }
    }

    // With nobody listening — every host that was handed no credential — the seam costs one
    // HasListeners check and does not even render the name.
    [Fact]
    public void Nothing_is_opened_when_nothing_listens()
    {
        Assert.Null(MessageSpan.Start(new MetadataProvider(), typeof(CreatePatient), null));

        using var recorder = new SpanRecorder();
        using var opened = MessageSpan.Start(new MetadataProvider(), typeof(CreatePatient), null);

        Assert.NotNull(opened);
    }
}
