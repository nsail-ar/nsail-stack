// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Configuration;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

public static class TenantKeys
{
    const string DevelopmentKey = "nsail-development-only-key";

    /// <summary>Registers the <see cref="KeyProtector"/> that seals with the current tenant's key,
    /// read from <c>Security:KeysDirectory</c>. A tenant with no key file makes a seal or an open
    /// throw. Development without a directory seals under a fixed key; any other environment
    /// without one refuses to compose.</summary>
    public static void AddTenantKeys(this WebApplicationBuilder builder)
    {
        var directory = builder.Configuration.Load<SecurityOptions>().KeysDirectory;
        IEncryptionKeySource source;

        if (!string.IsNullOrWhiteSpace(directory))
        {
            source = new TenantKeyFiles(directory, () => AmbientTenant.Current.Slug);
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Justified divergence: a developer machine has no provisioner writing key files, and
            // refusing to compose would stop every local run. The per-tenant path is the only one
            // a cell runs, so it is exercised by tests, not by Development.
            source = new DevelopmentKeySource();
        }
        else
        {
            throw new InvalidOperationException("Security:KeysDirectory is required outside Development: it holds the key of each tenant.");
        }

        builder.Services.AddSingleton(source);
        builder.Services.AddSingleton<KeyProtector>();
    }

    sealed class DevelopmentKeySource : IEncryptionKeySource
    {
        public string? Current
        {
            get { return DevelopmentKey; }
        }
    }
}
