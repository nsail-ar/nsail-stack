// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Microsoft.Extensions.Configuration;
using NSail.Data;
using NSail.Localization;

namespace NSail.Background.Tests;

/// <summary>nsail#1108: a reminder that never reached a phone. The job ran where no request
/// sets a culture, so it ran invariant, LanguageProvider seeded itself with "iv", and the
/// WhatsApp channel looked for a template in a language nobody has. The runner gives a job the
/// install's default language, read off the configuration the way the request edge reads it.</summary>
public sealed class BackgroundJobLanguageTests
{
    sealed class Observed
    {
        public string? Culture { get; set; }

        public string? Language { get; set; }
    }

    sealed class ObservingJob(Observed observed, LanguageProvider language, Signal signal) : IBackgroundJob
    {
        public TimeSpan Interval
        {
            get { return TimeSpan.FromHours(1); }
        }

        public TenancyScope Tenancy
        {
            get { return TenancyScope.Install; }
        }

        public Task Run(CancellationToken cancellationToken)
        {
            observed.Culture = CultureInfo.CurrentUICulture.Name;
            observed.Language = language.Current;

            signal.Record();

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_job_runs_in_the_installs_default_language()
    {
        var signal = new Signal();
        var observed = new Observed();

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Language:Default"] = "es" })
                .Build());
            services.AddLocalization();
            services.AddSingleton(signal);
            services.AddSingleton(observed);
            services.AddBackgroundJob<ObservingJob>();
        });

        await Harness.Run(provider, signal.Reached);

        Assert.Equal("es", observed.Culture);
        Assert.Equal("es", observed.Language);
    }

    // The harness that composes no host has no configuration and gets no opinion: the loop
    // keeps whatever culture the process has, the same as before.
    [Fact]
    public async Task A_host_with_no_configuration_leaves_the_culture_alone()
    {
        var signal = new Signal();
        var observed = new Observed();
        var ambient = CultureInfo.CurrentUICulture.Name;

        await using var provider = Harness.Build(services =>
        {
            services.AddLocalization();
            services.AddSingleton(signal);
            services.AddSingleton(observed);
            services.AddBackgroundJob<ObservingJob>();
        });

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(ambient, observed.Culture);
    }
}
