// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Messaging.Runtime;

namespace NSail.Security.Tests;

public sealed class ProviderCheckTests
{
    sealed class DbRelations : RelationProvider
    {
    }

    sealed class DbCredentials : CredentialProvider
    {
    }

    static Task Start(Action<IServiceCollection> compose)
    {
        var services = new ServiceCollection();
        compose(services);
        using var provider = services.BuildServiceProvider();
        return Task.WhenAll(provider.GetServices<IHostedService>().Select(h => h.StartAsync(default)));
    }

    [Fact]
    public async Task Enforcement_alone_refuses_to_start_naming_both_providers()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(s => s.AddSecurityEnforcement()));

        Assert.Contains("RelationProvider", ex.Message);
        Assert.Contains("CredentialProvider", ex.Message);
    }

    [Fact]
    public async Task Enforcement_with_both_providers_registered_starts_in_either_order()
    {
        await Start(s =>
        {
            s.AddSecurityEnforcement();
            s.AddScoped<RelationProvider, DbRelations>();
            s.AddScoped<CredentialProvider, DbCredentials>();
        });
        await Start(s =>
        {
            s.AddScoped<RelationProvider, DbRelations>();
            s.AddScoped<CredentialProvider, DbCredentials>();
            s.AddSecurityEnforcement();
        });
    }

    [Fact]
    public async Task Enforcement_with_one_provider_missing_still_refuses()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(s =>
        {
            s.AddSecurityEnforcement();
            s.AddScoped<RelationProvider, DbRelations>();
        }));

        Assert.Contains("CredentialProvider", ex.Message);
    }

    [Fact]
    public async Task Accepting_the_permissive_providers_starts()
    {
        await Start(s =>
        {
            s.AddSecurityEnforcement();
            s.AcceptPermissiveProviders();
        });
    }

    [Fact]
    public async Task A_host_without_enforcement_keeps_the_permissive_defaults()
    {
        await Start(s => s.AddSecurity());
    }
}
