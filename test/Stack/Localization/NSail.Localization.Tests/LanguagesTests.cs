// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Localization;

namespace NSail.Localization.Tests;

public sealed class LanguagesTests
{
    sealed class FakeSource(params string[] languages) : IStringSource
    {
        public IReadOnlyCollection<string> Languages { get; } = languages;

        public Task<IReadOnlyDictionary<string, string>> GetStrings(string language)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }

    static readonly LanguageOptions _spanishInstall = new() { Base = "en", Default = "es" };

    [Fact]
    public void Supported_carries_every_language_the_sources_overlay()
    {
        var supported = Languages.Supported([new FakeSource("es"), new FakeSource("pt")], _spanishInstall);

        Assert.Contains("es", supported);
        Assert.Contains("pt", supported);
    }

    // 40e145d9's finding: a source's Languages excludes the base language by contract, so an
    // install that defaults to Spanish used to leave its own English catalog unreachable —
    // whatever the browser asked for.
    [Fact]
    public void Supported_carries_the_base_language_no_default_names_it()
    {
        var supported = Languages.Supported([new FakeSource("es")], _spanishInstall);

        Assert.Contains("en", supported);
    }

    [Fact]
    public void Supported_carries_a_default_no_source_carries()
    {
        var supported = Languages.Supported([new FakeSource("es")], new LanguageOptions { Base = "en", Default = "fr" });

        Assert.Contains("fr", supported);
    }

    [Fact]
    public void Supported_names_each_language_once()
    {
        var supported = Languages.Supported([new FakeSource("es", "en"), new FakeSource("es")], _spanishInstall);

        Assert.Equal(["es", "en"], supported);
    }

    // The joint the whole story turns on: an install that says nothing about what it offers
    // offers one language, however many its catalogs can render. Supported carries English by
    // construction, and an install that can render English is not one that serves it.
    [Fact]
    public void An_install_that_declares_no_offer_offers_its_default_alone()
    {
        Assert.Equal(["es"], Languages.Offered(_spanishInstall));
    }

    [Fact]
    public void An_install_offers_what_it_declares()
    {
        var offered = Languages.Offered(new LanguageOptions { Base = "en", Default = "es", Offered = ["es", "en"] });

        Assert.Equal(["es", "en"], offered);
    }

    // A declared offer replaces the default rather than adding to it: a shop that names its
    // languages has said which ones, and an install whose default is not among them is a
    // configuration mistake to read plainly, not one to paper over.
    [Fact]
    public void A_declared_offer_does_not_carry_the_default_along()
    {
        var offered = Languages.Offered(new LanguageOptions { Base = "en", Default = "es", Offered = ["pt"] });

        Assert.Equal(["pt"], offered);
    }

    [Fact]
    public void An_offer_names_each_language_once_and_carries_no_blanks()
    {
        var offered = Languages.Offered(new LanguageOptions { Base = "en", Default = "es", Offered = ["es", "", "ES"] });

        Assert.Equal(["es"], offered);
    }

    [Fact]
    public void The_chosen_language_wins_over_the_default()
    {
        var culture = Languages.Negotiate("en", "es");

        Assert.Equal("en", culture!.Name);
    }

    // nsail#801: an óptica in Rosario read half English because the visitor's browser was, and
    // the install's own language was only the fallback. The browser is no longer an argument, so
    // the whole class of that bug is gone from this method's surface — this is what is left to
    // assert: with nothing chosen, the install answers, whatever the thread it is asked on.
    [Fact]
    public void The_install_answers_when_nobody_chose()
    {
        var culture = InCulture("en-US", () => Languages.Negotiate(null, "es"));

        Assert.Equal("es", culture!.Name);
    }

    [Fact]
    public void The_chosen_language_wins_on_a_browser_that_reads_another()
    {
        var culture = InCulture("en-US", () => Languages.Negotiate("es", "en"));

        Assert.Equal("es", culture!.Name);
    }

    // What a client with no answer at all sees: nothing to apply, so its caller leaves the
    // culture the runtime booted with rather than imposing a guess over it.
    [Fact]
    public void Nothing_is_negotiated_without_a_preference_or_a_default()
    {
        Assert.Null(Languages.Negotiate(null, null));
    }

    // Off the browser Adopt is a no-op by contract: a server's culture belongs to the request
    // carrying it, and a thread default set here would outlive that request. The test suite is
    // not a browser, which is exactly the condition being asserted.
    [Fact]
    public void A_culture_is_not_adopted_onto_a_thread_that_serves_requests()
    {
        var before = CultureInfo.DefaultThreadCurrentCulture;

        Languages.Adopt(CultureInfo.GetCultureInfo("es"));

        Assert.Equal(before, CultureInfo.DefaultThreadCurrentCulture);
    }

    static T InCulture<T>(string name, Func<T> act)
    {
        var previous = CultureInfo.CurrentCulture;

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        try
        {
            return act();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
