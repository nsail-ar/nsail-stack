// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2696 — the toolbar's own FILTROS toggle already carries the shared answer to
/// "is a filter applied with the panel closed": FilterCount renders as a badge on the toggle's
/// label, and the toggle opens and closes the Filter panel it was handed. Exercised here because
/// nothing did.</summary>
public sealed class NsPageHeaderFilterTests : BunitContext, IAsyncLifetime
{
    public NsPageHeaderFilterTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Filters"] = "Filtros",
            ["Common.FiltersCount"] = "Filtros ({0})",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void NoFilterCountReadsThePlainLabel()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Trabajos")
            .Add(header => header.Filter, Probe));

        var toggle = Assert.Single(cut.FindComponents<NsButton>(), button => button.Instance.Icon == NsIcons.Tune);

        Assert.Equal("Filtros", toggle.Instance.Label);
    }

    // A filter applied with the panel closed is invisible otherwise: the page computes the
    // count (only it knows what is non-default) and the toggle is where it has to say so.
    [Fact]
    public void ANonZeroFilterCountReadsAsACountedBadge()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Trabajos")
            .Add(header => header.FilterCount, 2)
            .Add(header => header.Filter, Probe));

        var toggle = Assert.Single(cut.FindComponents<NsButton>(), button => button.Instance.Icon == NsIcons.Tune);

        Assert.Equal("Filtros (2)", toggle.Instance.Label);
    }

    // Unbound, the toggle drives its own flag: the panel is closed until the reader presses it,
    // and pressing it again puts it away.
    [Fact]
    public async Task PressingTheUnboundToggleOpensAndClosesThePanel()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Trabajos")
            .Add(header => header.Filter, Probe));

        Assert.Empty(cut.FindAll(".probe-filter"));

        var button = ToggleButton(cut);

        await cut.InvokeAsync(() => button.Click());
        Assert.Single(cut.FindAll(".probe-filter"));

        await cut.InvokeAsync(() => button.Click());
        Assert.Empty(cut.FindAll(".probe-filter"));
    }

    // Bound, the page owns the flag and the press reports it rather than acting on its own —
    // the same two-way shape every other NSail toggle takes.
    [Fact]
    public async Task ABoundToggleReportsTheNextStateInsteadOfActingOnItsOwn()
    {
        var reported = new List<bool>();

        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Trabajos")
            .Add(header => header.Filter, Probe)
            .Add(header => header.FilterOpen, false)
            .Add(header => header.FilterOpenChanged, EventCallback.Factory.Create<bool>(this, next => reported.Add(next))));

        var button = ToggleButton(cut);

        await cut.InvokeAsync(() => button.Click());

        Assert.True(Assert.Single(reported));
        Assert.Empty(cut.FindAll(".probe-filter")); // the panel stays closed until the page re-renders with FilterOpen=true
    }

    // ui/styling.md: aria-expanded is the state a disclosure trigger owes, and the toggle is
    // one. Unbound so the press changes the state itself rather than waiting on a parameter the
    // page never hands back.
    [Fact]
    public async Task TheToggleReportsItsOwnOpenStateAsAriaExpanded()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Trabajos")
            .Add(header => header.Filter, Probe));

        var button = ToggleButton(cut);
        Assert.Equal("false", button.GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => button.Click());
        Assert.Equal("true", ToggleButton(cut).GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => button.Click());
        Assert.Equal("false", ToggleButton(cut).GetAttribute("aria-expanded"));
    }

    // A screen whose filters are required pins the panel open and offers no toggle to hide it —
    // there is nothing honest for the button to say about a choice the screen never gives.
    [Fact]
    public void AnAlwaysOpenFilterDrawsNoToggleAndStaysOpen()
    {
        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Cuenta corriente")
            .Add(header => header.Filter, Probe)
            .Add(header => header.FilterAlwaysOpen, true));

        Assert.DoesNotContain(cut.FindComponents<NsButton>(), button => button.Instance.Icon == NsIcons.Tune);
        Assert.Single(cut.FindAll(".probe-filter"));
    }

    // By icon and not cut.Find("button") — the first button in the tree is only the toggle
    // today, and a title row that grows one of its own (a guide door, a close) would make a
    // test written that way squeeze the wrong control while still reading green.
    static IElement ToggleButton(IRenderedComponent<NsPageHeader> cut)
    {
        var toggle = Assert.Single(cut.FindComponents<NsButton>(), button => button.Instance.Icon == NsIcons.Tune);
        return toggle.Find("button");
    }

    static void Probe(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "probe-filter");
        builder.CloseElement();
    }
}
