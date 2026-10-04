// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A date field says what shape it takes before anything is typed — and says it in
/// the ACTIVE culture's own order with the active language's own letters. Both halves are
/// derived: the order and separators from the culture's short-date pattern, the letters from
/// one translated string. Nothing here is hardcoded, which is the whole point: a third
/// language costs one dictionary entry.</summary>
public sealed class NsDatePlaceholderTests : BunitContext, IAsyncLifetime
{
    // MudBlazor services here are IAsyncDisposable-only, which bUnit's synchronous teardown
    // cannot dispose — the NsActionColumnTests rule.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    void Compose(string language)
    {
        // No MudPopoverProvider in this host: the vendor otherwise refuses to build the popover
        // a tooltip or a picker owns, and none of these assertions is about a popover.
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new TwoLanguageStrings("Common.DateLetters", "dmy", "dma")]));
        Services.AddSingleton(new LanguageProvider { Current = language });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void InSpanish_TheHintIsTheRioplatenseOrderWithSpanishLetters()
    {
        Compose("es");

        var cut = Render<NsDateField<DateOnly?>>();

        Assert.Equal("dd/mm/aaaa", cut.Find("input").GetAttribute("placeholder"));
    }

    [Fact]
    public void InEnglish_TheHintFollowsThatCulturesOwnOrder()
    {
        Compose("en");

        var cut = Render<NsDateField<DateOnly?>>();

        Assert.Equal("mm/dd/yyyy", cut.Find("input").GetAttribute("placeholder"));
    }

    /// <summary>A caller's own words still win — the derivation is the default, never a
    /// lock.</summary>
    [Fact]
    public void ACallersOwnPlaceholderStillWins()
    {
        Compose("es");

        var cut = Render<NsDateField<DateOnly?>>(ps => ps.Add(p => p.Placeholder, "Desde"));

        Assert.Equal("Desde", cut.Find("input").GetAttribute("placeholder"));
    }
}
