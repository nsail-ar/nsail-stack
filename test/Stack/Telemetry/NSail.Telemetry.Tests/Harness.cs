// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.BaseServices.WebApi;
using NSail.Consulting.Patients;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Sending;
using OpenTelemetry.Logs;

namespace NSail.Telemetry.Tests;

/// <summary>Captures the spans NSail's own source produces. An ActivityListener rather than a
/// TracerProvider on purpose: it measures the seam from outside, exactly as anything watching
/// a running host would, and it needs no exporter and no collector to do it.</summary>
public sealed class SpanRecorder : IDisposable
{
    readonly ActivityListener _listener;
    readonly List<Activity> _stopped = [];

    public SpanRecorder()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MessageSpan.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Add,
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> Stopped
    {
        get
        {
            lock (_stopped)
            {
                return _stopped.ToList();
            }
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    void Add(Activity activity)
    {
        lock (_stopped)
        {
            _stopped.Add(activity);
        }
    }
}

public static class Harness
{
    public const string Endpoint = "https://collector.invalid/otlp";

    public const string Credential = "Basic dGVzdDp0ZXN0";

    /// <summary>The real host bootstrap — AddBaseWebApi and nothing else, which is the
    /// contract — plus the senders a message needs to reach a handler.</summary>
    public static WebApplication Host(
        bool credentialed,
        Action<IServiceCollection>? compose = null,
        Action<OpenTelemetryLoggerOptions>? logging = null)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Endpoint"] = Endpoint,
        });

        var previous = Environment.GetEnvironmentVariable(TelemetryOptions.AuthorizationVariable);

        Environment.SetEnvironmentVariable(TelemetryOptions.AuthorizationVariable, credentialed ? Credential : null);

        try
        {
            builder.AddBaseWebApi();
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryOptions.AuthorizationVariable, previous);
        }

        if (logging is not null)
        {
            builder.Logging.AddOpenTelemetry(logging);
        }

        AddSenders(builder.Services);
        compose?.Invoke(builder.Services);

        return builder.Build();
    }

    public static Mediator Mediator(IServiceProvider services)
    {
        return services.GetRequiredService<Mediator>();
    }

    static void AddSenders(IServiceCollection services)
    {
        services.AddScoped<ISender<CreatePatient, Guid>, InProcessSender<CreatePatient, Guid>>();
        services.AddScoped<ISender<ArchivePatient>, InProcessSender<ArchivePatient>>();
        services.AddScoped<ISender<RefusedPatient>, InProcessSender<RefusedPatient>>();
        services.AddSingleton<IHandler<RefusedPatient>, RefusedPatientHandler>();
    }
}
