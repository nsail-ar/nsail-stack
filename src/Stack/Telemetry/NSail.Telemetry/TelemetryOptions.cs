// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Metadata;

namespace NSail.Telemetry;

/// <summary>Where telemetry goes and how much of it goes there, bound from the "Telemetry"
/// section. Endpoint, tenant and sampling are ordinary configuration and travel in
/// appsettings — one collector serves every product and environment, and those are told
/// apart by the resource attributes, not by a URL. The credential does not travel with
/// them: it arrives in the environment (the seam DefaultSmtp and ConnectionStrings already
/// use) and it arrives <b>pre-composed</b>, the whole header value including its scheme, so
/// nothing is concatenated here. An install handed no credential exports nothing and
/// collects nothing — that is what keeps a dev machine and CI silent without an
/// environment branch anywhere in the host.</summary>
public sealed class TelemetryOptions
{
    public const string AuthorizationVariable = "NSAIL_OTLP_AUTH";

    public string? Endpoint { get; set; }

    /// <summary>The collector's own account number, carried so a human debugging the
    /// section can read it. Nothing sends it: the backend identifies the install by the
    /// resource attributes and authenticates it by the header.</summary>
    public string? InstanceId { get; set; }

    /// <summary>Overrides the product name derived from the host assembly. An install with
    /// an NSail-shaped host name never sets it.</summary>
    public string? ServiceName { get; set; }

    public string? Tenant { get; set; }

    [Range(0d, 1d)]
    public double SamplingRatio { get; set; } = 1;

    /// <summary>Never bound from configuration in practice: the setup overwrites it with
    /// the environment variable, present or absent, so a credential typed into appsettings
    /// cannot win and cannot be persisted by accident.</summary>
    public string? Authorization { get; set; }

    public bool IsInForce
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Authorization);
        }
    }

    /// <summary>The product, derived through the same namespace convention every other key
    /// comes from rather than declared twice: the host assembly NSail.Optical.Web binds
    /// Area = Optical.</summary>
    public string ServiceFor(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        if (!string.IsNullOrWhiteSpace(ServiceName))
        {
            return ServiceName;
        }

        return new MetadataProvider().FromName(applicationName).Area ?? applicationName;
    }

    /// <summary>The install. A host handed no tenant answers with the machine it runs on,
    /// which is the honest answer and never an empty filter in the backend's face.</summary>
    public string TenantOrHost()
    {
        return string.IsNullOrWhiteSpace(Tenant) ? Environment.MachineName : Tenant;
    }

    /// <summary>Where one signal is posted. The configured endpoint is a collector's base —
    /// the gateway's own "/otlp" — and each signal hangs its own path off it; the exporter
    /// appends that path only for an endpoint it read from the environment itself, so an
    /// endpoint set in code has to arrive whole.</summary>
    public Uri EndpointFor(string signal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(Endpoint);

        return new Uri($"{Endpoint.TrimEnd('/')}/v1/{signal}");
    }
}
