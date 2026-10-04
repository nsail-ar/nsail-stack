// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Configuration;

namespace NSail.Telemetry.Tests;

public sealed class TelemetryOptionsTests
{
    [Fact]
    public void The_section_is_Telemetry_and_sampling_comes_out_of_it()
    {
        var options = Load(new Dictionary<string, string?>
        {
            ["Telemetry:Endpoint"] = Harness.Endpoint,
            ["Telemetry:InstanceId"] = "1804179",
            ["Telemetry:Tenant"] = "optical_lumina_test",
            ["Telemetry:SamplingRatio"] = "0.25",
        });

        Assert.Equal(Harness.Endpoint, options.Endpoint);
        Assert.Equal("1804179", options.InstanceId);
        Assert.Equal("optical_lumina_test", options.Tenant);
        Assert.Equal(0.25, options.SamplingRatio);
    }

    [Fact]
    public void Sampling_defaults_to_everything_and_refuses_a_ratio_that_is_not_one()
    {
        Assert.Equal(1, Load([]).SamplingRatio);

        var invalid = Assert.Throws<InvalidOperationException>(() =>
            Load(new Dictionary<string, string?> { ["Telemetry:SamplingRatio"] = "2" }));

        Assert.Contains("Telemetry.SamplingRatio", invalid.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_endpoint_without_a_credential_is_not_in_force()
    {
        var options = new TelemetryOptions { Endpoint = Harness.Endpoint };

        Assert.False(options.IsInForce);

        options.Authorization = Harness.Credential;

        Assert.True(options.IsInForce);
    }

    // The configured endpoint is a collector's base, and each signal has to arrive with its
    // own path already on it: the exporter appends one only for an endpoint it read from the
    // environment, so a base set in code would post traces, metrics and logs to the gateway's
    // root and collect three 404s.
    [Fact]
    public void Every_signal_hangs_its_own_path_off_the_configured_collector()
    {
        var options = new TelemetryOptions { Endpoint = "https://otlp-gateway.grafana.net/otlp" };

        Assert.Equal("https://otlp-gateway.grafana.net/otlp/v1/traces", options.EndpointFor("traces").ToString());
        Assert.Equal("https://otlp-gateway.grafana.net/otlp/v1/metrics", options.EndpointFor("metrics").ToString());
        Assert.Equal("https://otlp-gateway.grafana.net/otlp/v1/logs", options.EndpointFor("logs").ToString());

        options.Endpoint = "https://otlp-gateway.grafana.net/otlp/";

        Assert.Equal("https://otlp-gateway.grafana.net/otlp/v1/traces", options.EndpointFor("traces").ToString());
    }

    [Fact]
    public void The_service_name_is_the_product_the_host_assembly_names()
    {
        var options = new TelemetryOptions();

        Assert.Equal("Optical", options.ServiceFor("NSail.Optical.Web"));
        Assert.Equal("Therapy", options.ServiceFor("NSail.Therapy.Web"));

        options.ServiceName = "checkout";

        Assert.Equal("checkout", options.ServiceFor("NSail.Optical.Web"));
    }

    [Fact]
    public void An_install_that_names_no_tenant_answers_with_its_host()
    {
        Assert.Equal(Environment.MachineName, new TelemetryOptions().TenantOrHost());
        Assert.Equal("optical_lumina_test", new TelemetryOptions { Tenant = "optical_lumina_test" }.TenantOrHost());
    }

    static TelemetryOptions Load(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build().Load<TelemetryOptions>();
    }
}
