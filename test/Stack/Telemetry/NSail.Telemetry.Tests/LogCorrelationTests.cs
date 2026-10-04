// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.Consulting.Patients;
using NSail.Messaging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NSail.Telemetry.Tests;

// One traceId across a request's traces and its logs, and it is never hand-rolled: the
// OpenTelemetry logging provider stamps whatever Activity is ambient when the line is
// written, which inside a handler is the Mediator span. The host builds this same pipeline
// with the OTLP exporter behind it; here the collector is a processor that keeps the stamp.
public sealed class LogCorrelationTests
{
    [Fact]
    public async Task A_log_written_inside_a_send_carries_that_sends_traceId()
    {
        using var recorder = new SpanRecorder();
        var stamps = new TraceIdRecorder();

        using var factory = LoggerFactory.Create(logging =>
        {
            logging.AddOpenTelemetry(telemetry => telemetry.AddProcessor(stamps));
        });

        var logger = factory.CreateLogger("Consulting");
        var handler = new ArchivePatientHandler();

        using var app = Harness.Host(credentialed: false, services =>
        {
            services.AddSingleton<IHandler<ArchivePatient>>(handler);
        });

        handler.OnHandle = () => logger.LogInformation("Archived");

        await using var scope = app.Services.CreateAsyncScope();

        await Harness.Mediator(scope.ServiceProvider).Send(new ArchivePatient(Guid.NewGuid()));

        var span = Assert.Single(recorder.Stopped);
        var stamp = Assert.Single(stamps.Seen);

        Assert.NotEqual(default, span.TraceId);
        Assert.Equal(span.TraceId, stamp);
    }

    // A processor rather than the in-memory exporter: LogRecord instances are pooled and
    // reused, so what a test may keep is the value it reads on the way out, never the record.
    sealed class TraceIdRecorder : BaseProcessor<LogRecord>
    {
        public List<ActivityTraceId> Seen { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            ArgumentNullException.ThrowIfNull(data);

            Seen.Add(data.TraceId);
        }
    }
}
