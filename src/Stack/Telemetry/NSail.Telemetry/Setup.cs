// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSail.Background;
using NSail.Configuration;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Metadata;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NSail.Telemetry;

public static class Setup
{
    const string Namespace = "nsail";

    // .NET 10 ships Blazor's own instrumentation — aspnetcore.components.* spans and
    // instruments on these three names — so there is no package to add for it, only a
    // subscription. They cost nothing on a host that renders no component: a source
    // nothing emits on produces nothing.
    static readonly string[] _blazor =
    [
        "Microsoft.AspNetCore.Components",
        "Microsoft.AspNetCore.Components.Lifecycle",
        "Microsoft.AspNetCore.Components.Server.Circuits",
    ];

    /// <summary>Traces, metrics and logs for a server host, on one resource so a request's
    /// logs carry the trace's own traceId. The Mediator span is registered whatever the
    /// configuration says — an ActivitySource nobody listens to allocates nothing — and the
    /// exporter is registered only once a credential exists, so an install with none
    /// collects nothing and sends nothing.</summary>
    public static void AddTelemetry(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration.Load<TelemetryOptions>();

        options.Authorization = Environment.GetEnvironmentVariable(TelemetryOptions.AuthorizationVariable);

        builder.Services.AddMetadata();
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped(typeof(IInterceptor<>), typeof(TelemetryInterceptor<>));
        builder.Services.AddScoped(typeof(IInterceptor<,>), typeof(TelemetryInterceptor<,>));

        if (!options.IsInForce)
        {
            return;
        }

        var resource = Describe(builder, options);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resource);
            logging.AddProcessor(new LogScrubber());
            logging.AddOtlpExporter(exporter => Export(exporter, options, "logs"));
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resource);

                // Sampling is a number in the section, never a branch on the environment:
                // an install that wants everything and one that wants a tenth run the same
                // pipeline. ParentBased so a sampled caller's nested spans survive the roll.
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)));

                tracing.AddSource(MessageSpan.SourceName);
                tracing.AddSource(_blazor);
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();

                // No options lambda: the package offers none that suppresses the SQL text,
                // and the parameter values it can capture sit behind an
                // OTEL_DOTNET_EXPERIMENTAL_* variable nobody sets. What keeps a query out of
                // a span is the Scrubber below, which owes the vendor nothing.
                tracing.AddEntityFrameworkCoreInstrumentation();

                tracing.AddProcessor(new Scrubber());
                tracing.AddOtlpExporter(exporter => Export(exporter, options, "traces"));
            })
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resource);
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddMeter(_blazor);

                // The background runner's own three instruments. A metric and not the runner's
                // log lines, because LogScrubber drops every string attribute at this same
                // seam: the line that names the job would arrive with the job cut out of it,
                // and an alert has to name what broke.
                metrics.AddMeter(BackgroundJobMetrics.MeterName);

                metrics.AddOtlpExporter(exporter => Export(exporter, options, "metrics"));
            });
    }

    // The four attributes are the backend's own filters, so they are not free to invent.
    // InstanceId is deliberately not among them: it authenticates nothing and identifies
    // nothing the tenant attribute does not.
    static ResourceBuilder Describe(WebApplicationBuilder builder, TelemetryOptions options)
    {
        return ResourceBuilder.CreateDefault()
            .AddService(options.ServiceFor(builder.Environment.ApplicationName), serviceNamespace: Namespace)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
                new KeyValuePair<string, object>("nsail.tenant", options.TenantOrHost()),
            ]);
    }

    static void Export(OtlpExporterOptions exporter, TelemetryOptions options, string signal)
    {
        exporter.Endpoint = options.EndpointFor(signal);
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;

        // The variable holds the finished header value, scheme included. Composing one here
        // would mean this code knows how the collector authenticates, and the day it stops
        // being Basic the seam would be a code change instead of a deploy variable.
        exporter.Headers = $"Authorization={options.Authorization}";
    }
}
