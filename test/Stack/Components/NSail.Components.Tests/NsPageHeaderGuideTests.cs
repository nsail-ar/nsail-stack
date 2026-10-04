// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#929: a page adds its door to the guide with one attribute, and the header
/// is what places it — last in the title row, past whatever utilities the page announced — so
/// every screen's way into the guide is in the same corner.</summary>
public sealed class NsPageHeaderGuideTests : BunitContext, IAsyncLifetime
{
    public NsPageHeaderGuideTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Guide"] = "Ayuda",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddScoped(provider => new GuideRoute(
            typeof(ProbeGuidePage),
            provider.GetRequiredService<RouteTable>()));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("someone");
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RenderFragment Utility()
    {
        return builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "probe-utility");
            builder.CloseElement();
        };
    }

    [Fact]
    public void TheHeaderPlacesTheDoorItselfPastThePagesOwnUtilities()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Órdenes de Trabajo")
            .Add(header => header.Caption, Utility())
            .Add(header => header.Guide, "work-orders#cobrar-una-ot"));

        var guide = cut.FindComponent<NsGuideLink>();

        Assert.Equal("/?aside=guide%2Fwork-orders#cobrar-una-ot", guide.Find("a").GetAttribute("href"));

        // Both live in the title row's own action stack, and the door comes last: the page's
        // utilities keep the places they had and the guide sits at the corner.
        var row = cut.Find(".probe-utility").ParentElement!;

        Assert.Single(row.QuerySelectorAll(".probe-utility"));
        Assert.Single(row.QuerySelectorAll("a"));
        Assert.NotNull(row.LastElementChild!.QuerySelector("a"));
    }

    [Fact]
    public void AHeaderThatNamesNoTopicDrawsNoDoor()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Órdenes de Trabajo"));

        Assert.Empty(cut.FindComponents<NsGuideLink>());
    }
}
