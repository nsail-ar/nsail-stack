// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>An address that arrives written is never trusted on its scheme: NsLink and NsChip
/// draw javascript: and data: as inert text, an empty Href as a disabled link, and keep
/// https:, mailto:, tel: and in-app routes as links.</summary>
public sealed class NsLinkSchemeTests : BunitContext
{
    public NsLinkSchemeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo("/probe");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    public void NsLinkDrawsAForbiddenSchemeWithNoHref(string href)
    {
        var cut = Render<NsLink>(p => p.Add(x => x.Href, href).Add(x => x.Label, "Sitio"));

        Assert.Empty(cut.FindAll("[href]"));
        Assert.Contains("Sitio", cut.Markup);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    public void NsChipDrawsAForbiddenSchemeWithNoHref(string href)
    {
        var cut = Render<NsChip>(p => p.Add(x => x.Text, "Sitio").Add(x => x.Href, href));

        Assert.Empty(cut.FindAll("[href]"));
    }

    [Fact]
    public void AnEmptyHrefRendersDisabledInsteadOfThrowing()
    {
        var cut = Render<NsLink>(p => p.Add(x => x.Href, "").Add(x => x.Label, "Sitio").Add(x => x.As, NsAs.Default));

        Assert.Empty(cut.FindAll("[href]"));
        Assert.NotEmpty(cut.FindAll("[disabled]"));
    }

    [Theory]
    [InlineData("https://acme.com/x")]
    [InlineData("mailto:a@acme.com")]
    [InlineData("tel:+5491100000000")]
    [InlineData("/parties/new")]
    public void TheSchemesADoorOpensKeepTheirHref(string href)
    {
        var cut = Render<NsLink>(p => p.Add(x => x.Href, href).Add(x => x.Label, "Sitio"));

        Assert.NotEmpty(cut.FindAll("a[href]"));
    }
}
