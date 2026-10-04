// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Consulting.Patients;
using NSail.Messaging.Runtime.Pipelines;
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
}
