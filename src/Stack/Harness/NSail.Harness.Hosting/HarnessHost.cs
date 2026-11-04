// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using NSail.Harness.Lanes;
using NSail.Telemetry;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;

namespace NSail.Harness.Hosting;

// What every harness host is, whatever its project: a Windows service under the SCM, a daemon under
// systemd, a console app anywhere else; machine-local settings read last; a daily log file; the API,
// its reference and the status page. A host is a plain WebApplication that adds this and its
// providers (AddGitHub, AddClaude, ...).
public static class HarnessHost
{
    public static WebApplicationBuilder AddHarness(this WebApplicationBuilder builder)
    {
        // A service starts in system32: its settings are read from where the binary lives, below
        // everything else, whatever the content root turned out to be.
        var home = AppContext.BaseDirectory;
        if (!Path.GetFullPath(builder.Environment.ContentRootPath).TrimEnd('\\', '/').Equals(Path.GetFullPath(home).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.Sources.Insert(0, new JsonConfigurationSource { Path = "appsettings.json", Optional = true, FileProvider = new PhysicalFileProvider(home) });
        }
        builder.Configuration.Sources.Add(new JsonConfigurationSource { Path = "appsettings.Local.json", Optional = true, FileProvider = new PhysicalFileProvider(home) });

        builder.Services.AddWindowsService(o => o.ServiceName = "Harness");
        builder.Services.AddSystemd();
        var workRoot = builder.Configuration.GetSection(HarnessOptions.Section).Get<HarnessOptions>()?.WorkRoot ?? new HarnessOptions().WorkRoot;
        builder.Logging.AddProvider(new FileLog(Path.Combine(workRoot, "logs")));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddHarness(builder.Configuration);
        // Logs, traces and metrics to the configured collector once a credential exists
        // (NSAIL_OTLP_AUTH); with none, nothing is sent. The harness's own instruments ride along.
        builder.AddTelemetry();
        builder.Services.ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(HarnessMetrics.MeterName));
        builder.Services.AddOpenApi();
        return builder;
    }

    public static WebApplication MapHarness(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        app.MapHarnessApi();
        app.MapPages();
        return app;
    }
}
