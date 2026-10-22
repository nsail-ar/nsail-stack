// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using NSail.Consulting.Patients;
using NSail.Data;
using NSail.Messaging.Runtime.Pipelines;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NSail.Telemetry.Tests;

// AddBaseWebApi and nothing else: no app wires telemetry, and no environment branch decides
// whether it is on — the credential does.
public sealed class TelemetryHostTests
{
    [Fact]
    public void A_host_with_no_credential_boots_with_the_whole_thing_inert()
    {
        using var app = Harness.Host(credentialed: false);

        Assert.False(app.Services.GetRequiredService<TelemetryOptions>().IsInForce);
        Assert.Null(app.Services.GetService<TracerProvider>());
    }

    [Fact]
    public void A_host_handed_a_credential_exports()
    {
        using var app = Harness.Host(credentialed: true);

        var options = app.Services.GetRequiredService<TelemetryOptions>();

        Assert.True(options.IsInForce);
        Assert.Equal(Harness.Endpoint, options.Endpoint);
        Assert.Equal(Harness.Credential, options.Authorization);
        Assert.NotNull(app.Services.GetService<TracerProvider>());
    }

    // The read side hangs off its own credential, and this is the branch AC4's structural half
    // stands on: an install handed none resolves NO reader, so the door that would have used
    // one says it cannot answer. Registered instead, its SearchUri() would throw
    // ArgumentException on a missing endpoint, which is not a sentence anybody can read.
    [Fact]
    public void A_host_with_neither_read_endpoint_nor_read_credential_resolves_no_failure_log()
    {
        using var app = Harness.Host(credentialed: true);

        Assert.False(app.Services.GetRequiredService<TelemetryOptions>().CanSearch);
        Assert.Null(app.Services.GetService<IFailureLog>());
    }

    // The span is registered either way: an ActivitySource nobody listens to costs nothing,
    // and a pipeline that changed shape with the configuration would be the environment
    // branch baseservices.md forbids, wearing a different hat.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_mediator_span_is_registered_whatever_the_configuration_says(bool credentialed)
    {
        using var app = Harness.Host(credentialed);

        using var scope = app.Services.CreateScope();

        Assert.Contains(
            scope.ServiceProvider.GetServices<IInterceptor<ArchivePatient>>(),
            interceptor => interceptor is TelemetryInterceptor<ArchivePatient>);

        Assert.Contains(
            scope.ServiceProvider.GetServices<IInterceptor<CreatePatient, Guid>>(),
            interceptor => interceptor is TelemetryInterceptor<CreatePatient, Guid>);
    }

    // A module this one cannot reference names its meter at the composition that has both, and
    // that name is the whole of the subscription — nothing else in the pipeline would say it had
    // been dropped. Measured on an instrument, which reports whether a listener took its meter,
    // and on a meter nobody names, so that "subscribed" is not read off "everything is".
    [Fact]
    public void A_meter_named_at_the_composition_is_subscribed_and_one_nobody_names_is_not()
    {
        using var app = Harness.Host(credentialed: true);

        Assert.NotNull(app.Services.GetService<MeterProvider>());

        using var data = new Meter(TenantPolicies.MeterName);
        using var unnamed = new Meter("NSail.Telemetry.Tests.Unnamed");

        Assert.True(data.CreateCounter<long>("probe").Enabled);
        Assert.False(unnamed.CreateCounter<long>("probe").Enabled);
    }
}
