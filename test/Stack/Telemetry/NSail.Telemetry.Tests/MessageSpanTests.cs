// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Consulting.Patients;
using NSail.Messaging;
using NSail.Metadata;
using NSail.Problems;

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

    // With nobody listening — every host that was handed no credential — the seam costs one
    // HasListeners check and does not even render the name.
    [Fact]
    public void Nothing_is_opened_when_nothing_listens()
    {
        Assert.Null(MessageSpan.Start(new MetadataProvider(), typeof(CreatePatient)));

        using var recorder = new SpanRecorder();
        using var opened = MessageSpan.Start(new MetadataProvider(), typeof(CreatePatient));

        Assert.NotNull(opened);
    }
}
