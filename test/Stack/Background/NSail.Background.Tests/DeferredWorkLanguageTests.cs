// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Microsoft.Extensions.Configuration;
using NSail.Data;
using NSail.Localization;

namespace NSail.Background.Tests;

/// <summary>nsail#1550: an order confirmation that never reached a phone. The queue's runner
/// enters a scope with a session and a tenant and no culture, so the item ran invariant,
/// LanguageProvider seeded itself with "iv", and the WhatsApp channel refused the template
/// before the transport because no account holds one in a language nobody has. The runner gives
/// a deferred item the install's default language, the same way nsail#1108 gave it to a job.</summary>
public sealed class DeferredWorkLanguageTests
{
    sealed class Observed
    {
        public string? Culture { get; set; }

        public string? Language { get; set; }
    }

    static void Enqueue(ServiceProvider provider, Observed observed, Signal signal)
    {
        provider.GetRequiredService<DeferredWork>().Enqueue("Tests.Observe", Tenant.None, (services, _) =>
        {
            observed.Culture = CultureInfo.CurrentUICulture.Name;
            observed.Language = services.GetRequiredService<LanguageProvider>().Current;

            signal.Record();

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Deferred_work_runs_in_the_installs_default_language()
    {
        var signal = new Signal();
        var observed = new Observed();

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Language:Default"] = "es" })
                .Build());
            services.AddLocalization();
            services.AddDeferredWork();
        });

        Enqueue(provider, observed, signal);

        await Harness.Run(provider, signal.Reached);

        Assert.Equal("es", observed.Culture);
        Assert.Equal("es", observed.Language);
    }

    // The harness that composes no host has no configuration and gets no opinion: the item
    // keeps whatever culture the process has, which is also what HandlerHost's inline drain
    // leaves it on.
    [Fact]
    public async Task A_host_with_no_configuration_leaves_the_culture_alone()
    {
        var signal = new Signal();
        var observed = new Observed();
        var ambient = CultureInfo.CurrentUICulture.Name;

        await using var provider = Harness.Build(services =>
        {
            services.AddLocalization();
            services.AddDeferredWork();
        });

        Enqueue(provider, observed, signal);

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(ambient, observed.Culture);
    }
}
