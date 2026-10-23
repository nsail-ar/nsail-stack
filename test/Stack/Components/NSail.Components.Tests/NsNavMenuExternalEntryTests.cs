// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A contributor's entry naming a Url instead of a PageType: the seam an external
/// destination rides on, since the menu contract otherwise resolves every address through the
/// route table (nsail#2497). Drawn with no permission beyond being signed in, never tracked as
/// the active route, and always opened in a new window — leaving no surface behind would leave
/// the app, which this door never does.</summary>
public sealed class NsNavMenuExternalEntryTests : BunitContext, IAsyncLifetime
{
    // The drawer draws a tooltip (the "?" door), and the vendor's popover service behind one is
    // IAsyncDisposable only — a synchronous dispose of the container throws on it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<NsNavMenu> RenderMenu(NavMenuItem entry)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuExternalEntryTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(entry));
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("external-probe");

        return Render<NsNavMenu>();
    }

    [Fact]
    public void AnEntryNamingAUrlIsDrawnAsALinkToIt()
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Observability", Url = "https://telemetry.nsail.ar" });

        var link = menu.Find(".ns-nav-entry a");

        Assert.Equal("https://telemetry.nsail.ar", link.GetAttribute("href"));
        Assert.Contains("Observability", link.TextContent);
    }

    [Fact]
    public void ItAlwaysOpensInANewWindow()
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Observability", Url = "https://telemetry.nsail.ar" });

        Assert.Equal("_blank", menu.Find(".ns-nav-entry a").GetAttribute("target"));
    }

    [Fact]
    public void AnEntryNamingNeitherAPageNorAUrlDrawsNothing()
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Nowhere" });

        Assert.Empty(menu.FindAll(".ns-nav-entry"));
    }

    // A path of this app's own is a destination too — the one leaf whose address the route table
    // cannot answer for, an endpoint no page hosts.
    [Fact]
    public void AnEntryNamingThisAppsOwnPathIsDrawnAsALinkToIt()
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Observability", Url = "/health" });

        Assert.Equal("/health", menu.Find(".ns-nav-entry a").GetAttribute("href"));
    }

    // And an address that no route table could have produced is no destination: the entry goes
    // where one naming nowhere goes, so a scheme a module contributed never reaches the href.
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("//evil.example/looks-like-ours")]
    // The one leading slash of a path of our own, and then the slant the browser reads as the
    // second: "/\evil.example" resolves to another host, so it is the protocol-relative case.
    [InlineData("/\\evil.example/looks-like-ours")]
    public void AnEntryWhoseUrlCouldNotBecomeAnHrefDrawsNothing(string url)
    {
        var menu = RenderMenu(new NavMenuItem { Name = "Observability", Url = url });

        Assert.Empty(menu.FindAll(".ns-nav-entry"));
    }
}
