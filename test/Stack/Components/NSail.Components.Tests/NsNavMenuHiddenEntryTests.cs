// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/hidden/kept")]
public sealed class HiddenKeptProbePage : ComponentBase;

[Route("/hidden/dropped")]
public sealed class HiddenDroppedProbePage : ComponentBase;

/// <summary>An app hides a kit's door by contributing the same Name with Visible false, and the
/// drawer is where that lands: the entry goes and its whole branch goes with it, without the
/// permission gate ever being asked about the leaves under it. The tree keeps them — that is
/// what leaves NavMenuItem.Find able to answer, so a page reached some other way still has a
/// glyph on its own title bar.</summary>
public sealed class NsNavMenuHiddenEntryTests : BunitContext
{
    IRenderedComponent<NsNavMenu> RenderMenu()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuHiddenEntryTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Kept", PageType = typeof(HiddenKeptProbePage) },
            new NavMenuItem
            {
                Name = "Dropped",
                Items = [new NavMenuItem { Name = "Inside", PageType = typeof(HiddenDroppedProbePage) }]
            }));
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Dropped", Visible = false }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("hidden-probe");

        return Render<NsNavMenu>();
    }

    [Fact]
    public void TheHiddenEntryIsNotDrawn()
    {
        Assert.DoesNotContain("Dropped", RenderMenu().Markup);
    }

    [Fact]
    public void TheBranchUnderItGoesToo()
    {
        Assert.DoesNotContain("Inside", RenderMenu().Markup);
    }

    [Fact]
    public void WhatTheAppDidNotHideStaysDrawn()
    {
        Assert.Contains("Kept", RenderMenu().Markup);
    }

    [Fact]
    public void TheHiddenBranchsPageStillHasAMenuEntryToDeriveAGlyphFrom()
    {
        var cut = RenderMenu();
        var menu = cut.Services.GetRequiredService<NavMenu>();

        var items = menu.GetItems(null).GetAwaiter().GetResult();

        Assert.NotNull(NavMenuItem.Find(items, typeof(HiddenDroppedProbePage)));
    }
}
