// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NSail.Security;

// Registered by AcceptPermissiveProviders: the host knows enforcement runs over the
// permissive Relation and Credential providers and says so.
sealed class PermissiveProvidersAccepted
{
}

// Enforcement over the permissive base passes every relation constraint and honours every
// stale ticket, so a host that forgot Iam must not start quietly. Read from the descriptors
// rather than by resolving: the DB-backed providers are scoped and need a request.
sealed class SecurityProviderCheck : IHostedService
{
    readonly IServiceCollection _services;
    readonly bool _accepted;

    public SecurityProviderCheck(IServiceCollection services, PermissiveProvidersAccepted? accepted = null)
    {
        _services = services;
        _accepted = accepted is not null;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_accepted)
        {
            return Task.CompletedTask;
        }

        var missing = new List<string>();
        foreach (var provider in new[] { typeof(RelationProvider), typeof(CredentialProvider) })
        {
            var last = _services.LastOrDefault(d => d.ServiceType == provider);
            if (last is null || last.ImplementationType == provider)
            {
                missing.Add(provider.Name);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"AddSecurityEnforcement is on but {string.Join(" and ", missing)} {(missing.Count > 1 ? "are" : "is")} still the permissive default, " +
                "so every relation constraint passes and stale tickets are honoured. Register the Iam-backed " +
                "RelationProvider and CredentialProvider, or call AcceptPermissiveProviders() if this host has no Iam.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
