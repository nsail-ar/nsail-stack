// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NSail.BaseServices.WebApp.Tests;

/// <summary>nsail#801: an install in Rosario served English to anyone whose browser was set to
/// it, because ASP.NET's default provider chain lets Accept-Language outvote the install. The
/// promise nobody was watching is that a shop's screens read in the shop's language — so the
/// reproduction is the pin: a request that asks in English at an install that speaks Spanish.
/// What the browser votes among is what the install OFFERS, which is why the same browser is
/// answered in English one install over. The pipeline is the real one (UseBaseWebApp), asked at
/// the one place a culture is visible from outside: what the request ends up running in.</summary>
public sealed class RequestLanguageTests
{
    const string Culture = "/culture";

    [Fact]
    public async Task A_browser_asking_in_English_is_still_served_the_installs_language()
    {
        await using var host = await Start("es");

        Assert.Equal("es", await host.Read(browser: "en-US", chosen: null));
    }

    [Fact]
    public async Task A_browser_that_asks_for_nothing_is_served_the_installs_language()
    {
        await using var host = await Start("es");

        Assert.Equal("es", await host.Read(browser: null, chosen: null));
    }

    [Fact]
    public async Task A_chosen_language_outranks_the_install_and_the_browser_both()
    {
        await using var host = await Start("es");

        Assert.Equal("en", await host.Read(browser: "es-AR", chosen: "en"));
    }

    // The two sets are not one: a forced choice may name any language the catalogs render, which
    // is wider than what this install puts in front of somebody who chose nothing.
    [Fact]
    public async Task A_chosen_language_the_install_does_not_offer_is_still_served()
    {
        await using var host = await Start("es", offered: ["es"]);

        Assert.Equal("en", await host.Read(browser: "es-AR", chosen: "en"));
    }

    // The other half of the story: the browser is a voter again, and an install that attends in
    // two languages answers the machine in the one its owner reads.
    [Fact]
    public async Task A_browser_asking_in_English_is_served_it_where_the_install_offers_English()
    {
        await using var host = await Start("es", offered: ["es", "en"]);

        Assert.Equal("en", await host.Read(browser: "en-US", chosen: null));
    }

    // The parent-culture walk on the offer's side: an offer names a language, a browser asks in
    // a regional tag of it, and "en" is what "en-US" is asking for.
    [Fact]
    public async Task A_regional_tag_votes_for_the_language_the_install_offers()
    {
        await using var host = await Start("es", offered: ["es", "en"]);

        Assert.Equal("en", await host.Read(browser: "en-GB,en;q=0.9", chosen: null));
    }

    // The browser's order survives the narrowing — its second choice is counted when its first
    // is not on offer, rather than the whole ballot being dropped for the install's default.
    [Fact]
    public async Task The_browsers_next_choice_wins_when_its_first_is_not_offered()
    {
        await using var host = await Start("es", offered: ["es", "en"]);

        Assert.Equal("en", await host.Read(browser: "de-DE,en;q=0.8", chosen: null));
    }

    [Fact]
    public async Task A_browser_asking_for_nothing_the_install_offers_is_served_the_default()
    {
        await using var host = await Start("es", offered: ["es", "en"]);

        Assert.Equal("es", await host.Read(browser: "pt-BR", chosen: null));
    }

    // The words and the separators come out of one CultureInfo, never two: the split the story
    // reports is Spanish menus over 580,064.00, and it can only happen if UICulture and Culture
    // are resolved apart.
    [Fact]
    public async Task The_words_and_the_numbers_come_from_one_culture()
    {
        await using var host = await Start("es");

        var response = await host.Get(browser: "en-US", chosen: null);

        Assert.Equal("es|es|580.064,00", response);
    }

    // And on the path the browser wins, which is the one the story adds: a vote that moved the
    // menus without the separators would be the same defect wearing the other language.
    [Fact]
    public async Task The_browsers_vote_moves_the_words_and_the_numbers_together()
    {
        await using var host = await Start("es", offered: ["es", "en"]);

        var response = await host.Get(browser: "en-US", chosen: null);

        Assert.Equal("en|en|580,064.00", response);
    }

    static Task<LanguageHost> Start(string installDefault, params string[] offered)
    {
        return LanguageHost.Start(installDefault, offered);
    }

    sealed class LanguageHost : IAsyncDisposable
    {
        readonly WebApplication _app;

        LanguageHost(WebApplication app)
        {
            _app = app;
        }

        HttpClient Client { get; set; } = null!;

        public static async Task<LanguageHost> Start(string installDefault, string[] offered)
        {
            // Production for the same reason ProblemDocumentHost runs there: Development turns
            // on container validation that wants an identity stack this suite has no business
            // composing.
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
            });

            builder.WebHost.UseTestServer();

            // The offer arrives the way an install declares it, as indexed configuration keys —
            // a host handed a bound object would prove the middleware over a set no appsettings
            // could produce.
            var configuration = new Dictionary<string, string?>
            {
                ["Language:Base"] = "en",
                ["Language:Default"] = installDefault,
            };

            for (var index = 0; index < offered.Length; index++)
            {
                configuration[$"Language:Offered:{index}"] = offered[index];
            }

            builder.Configuration.AddInMemoryCollection(configuration);

            builder.AddBaseWebApp();

            var app = builder.Build();

            // UseRequestLanguage and nothing else of UseBaseWebApp, for ProblemDocumentHost's
            // own reason: MapStaticAssets wants a manifest only a real web host builds.
            app.UseRequestLanguage();

            app.MapGet(Culture, () =>
            {
                return string.Join(
                    '|',
                    CultureInfo.CurrentUICulture.Name,
                    CultureInfo.CurrentCulture.Name,
                    580064m.ToString("N2", CultureInfo.CurrentCulture));
            });

            await app.StartAsync();

            return new LanguageHost(app) { Client = app.GetTestClient() };
        }

        public async Task<string> Get(string? browser, string? chosen)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Culture);

            if (browser is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", browser);
            }

            if (chosen is not null)
            {
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    $"{CookieRequestCultureProvider.DefaultCookieName}={CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(chosen))}");
            }

            var response = await Client.SendAsync(request);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> Read(string? browser, string? chosen)
        {
            return (await Get(browser, chosen)).Split('|')[0];
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();

            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
