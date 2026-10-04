// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Consulting.Patients;
using NSail.Messaging;
using NSail.Problems;

namespace NSail.Telemetry.Tests;

// What makes "no personal data in telemetry" true is shape, not discipline: the seam that
// emits has nothing personal to hand over. The message below carries a name, a document
// number and a clinical note, and the span it produces can only name the operation and how
// it ended.
public sealed class SpanScrubTests
{
    static readonly string[] _allowed = [MessageSpan.OutcomeTag, MessageSpan.ProblemTag];

    const string Name = "Juan Perez";

    const string Document = "20123456789";

    const string Clinical = "refiere cefalea desde marzo";

    [Fact]
    public async Task A_spans_tags_are_a_closed_list_of_outcomes_and_codes()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<CreatePatient, Guid>>(new CreatePatientHandler());
        });

        await using var scope = app.Services.CreateAsyncScope();

        await Harness.Mediator(scope.ServiceProvider).Send(new CreatePatient(Name, Document, Clinical));

        var span = Assert.Single(recorder.Stopped);

        Assert.Empty(span.TagObjects.Select(tag => tag.Key).Except(_allowed));
    }

    [Fact]
    public async Task Nothing_a_message_carries_reaches_a_span()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<CreatePatient, Guid>>(new CreatePatientHandler());
        });

        await using var scope = app.Services.CreateAsyncScope();

        await Harness.Mediator(scope.ServiceProvider).Send(new CreatePatient(Name, Document, Clinical));

        var span = Assert.Single(recorder.Stopped);

        var written = string.Join(
            '|',
            span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}").Append(span.DisplayName).Append(span.StatusDescription));

        Assert.DoesNotContain(Name, written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Document, written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Clinical, written, StringComparison.OrdinalIgnoreCase);
    }

    // A refusal is the sharp case: the Problem's Title is free to quote the value that caused
    // it, and only its Code travels. Nothing calls RecordException and nothing sets a status
    // description, so the exception's own message has no way onto the span either.
    [Fact]
    public async Task A_refusal_carries_its_code_and_not_its_message()
    {
        using var recorder = new SpanRecorder();
        using var app = Harness.Host(credentialed: false);

        await using var scope = app.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<BusinessException>(() =>
            Harness.Mediator(scope.ServiceProvider).Send(new RefusedPatient()));

        var span = Assert.Single(recorder.Stopped);

        Assert.Empty(span.TagObjects.Select(tag => tag.Key).Except(_allowed));
        Assert.Empty(span.Events);
        Assert.Null(span.StatusDescription);
        Assert.DoesNotContain(Name, string.Join('|', span.TagObjects.Select(tag => tag.Value)), StringComparison.OrdinalIgnoreCase);
    }
}
